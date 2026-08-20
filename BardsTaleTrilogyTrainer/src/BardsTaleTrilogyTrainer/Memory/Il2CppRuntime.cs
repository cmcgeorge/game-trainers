using System.IO;
using System.Runtime.InteropServices;
using BardsTaleTrilogyTrainer.Game;
using Microsoft.Win32.SafeHandles;

namespace BardsTaleTrilogyTrainer.Memory;

/// <summary>
/// Calls a handful of <c>GameAssembly.dll</c>'s exported IL2CPP runtime functions inside the
/// game process, which is the only way to get it to allocate a managed object on the trainer's
/// behalf.
///
/// <para>This exists for one job: growing a character's <c>m_learntSpells</c> list. A
/// <c>List&lt;T&gt;</c> starts out sharing a zero-length backing array, so a character who was
/// never taught a script or quest spell has nowhere to append to, and no amount of
/// <c>WriteProcessMemory</c> can conjure a garbage-collected array. Everything else the trainer
/// does is a plain read or write; this path is only taken when the fast one cannot fire.</para>
///
/// <para>Only <b>exported</b> functions are called — <c>il2cpp_domain_get</c>,
/// <c>il2cpp_thread_attach</c>, <c>il2cpp_gc_disable</c>/<c>_enable</c>,
/// <c>il2cpp_array_new_specific</c>, <c>il2cpp_thread_detach</c> — resolved from the module's
/// own export table at run time. Nothing depends on a game-version-specific address, so a patched
/// build cannot send the injected thread somewhere arbitrary; at worst an export is missing and
/// the call is refused. The array's type comes from the class pointer in the header of the array
/// being replaced, so the runtime allocates exactly the type the field already holds.</para>
///
/// <para>Nothing is written to disk. A scratch page is committed in the game, a short stub is
/// written to it and run once on a new thread, and the page is released afterwards.</para>
/// </summary>
public sealed class Il2CppRuntime : IDisposable
{
    private const uint PROCESS_CREATE_THREAD = 0x0002;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_VM_READ = 0x0010;

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    private const uint WAIT_OBJECT_0 = 0;

    /// <summary>How long a stub is given to finish. It only makes a few runtime calls.</summary>
    private const uint CallTimeoutMs = 5000;

    /// <summary>
    /// Scratch layout: the allocated array lands at +0, the attached thread at +8, and the
    /// stub's "I have disabled the collector" marker at +0x10.
    /// </summary>
    private const int ScratchResult = 0;
    private const int ScratchThread = 8;
    private const int ScratchGcOff = 0x10;
    private const int ScratchSize = 0x18;
    private const int StubOffset = 0x20;
    private const int PageSize = 0x1000;

    private readonly SafeProcessHandle _process;
    private readonly IMemorySource _mem;
    private readonly Dictionary<string, nuint> _exports;

    private Il2CppRuntime(SafeProcessHandle process, IMemorySource mem, Dictionary<string, nuint> exports)
    {
        _process = process;
        _mem = mem;
        _exports = exports;
    }

    /// <summary>The exports the growth path needs; all of them must be present for it to run.</summary>
    private static readonly string[] Required =
    {
        "il2cpp_domain_get", "il2cpp_thread_attach", "il2cpp_thread_detach",
        "il2cpp_gc_disable", "il2cpp_gc_enable", "il2cpp_array_new_specific",
    };

    /// <summary>
    /// Opens the game for injection and resolves the runtime exports. Returns null — rather than
    /// throwing — when the process cannot be opened with the needed rights or the module does not
    /// export what is required, so callers can simply fall back to reporting the limitation.
    /// </summary>
    public static Il2CppRuntime? TryOpen(int processId, IMemorySource mem, nuint moduleBase, out string error)
    {
        error = "";
        if (moduleBase == 0)
        {
            error = "GameAssembly.dll was not located in the process.";
            return null;
        }

        Dictionary<string, nuint> exports;
        try
        {
            exports = ReadExports(mem, moduleBase);
        }
        catch (Exception ex)
        {
            error = $"could not read GameAssembly.dll's export table ({ex.Message}).";
            return null;
        }

        var missing = Required.Where(name => !exports.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            error = $"GameAssembly.dll does not export {string.Join(", ", missing)}.";
            return null;
        }

        var handle = OpenProcess(
            PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
            PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            error = "the game could not be opened with thread-creation rights (run the trainer as administrator).";
            return null;
        }

        return new Il2CppRuntime(handle, mem, exports);
    }

    /// <summary>
    /// Allocates a managed array of the same type as <paramref name="templateArray"/> and leaves
    /// the collector disabled, so the result stays alive until <see cref="ResumeCollection"/> is
    /// called. The caller is expected to store the returned reference into a live object first —
    /// nothing else refers to it, and the collector would otherwise be free to take it back.
    /// </summary>
    /// <returns>The new array's address, or 0 when the call could not be made.</returns>
    public nuint AllocateArrayLike(nuint templateArray, int length, out string error)
    {
        error = "";
        if (templateArray == 0 || length <= 0)
        {
            error = "no template array to take the element type from.";
            return 0;
        }

        nuint klass = _mem.ReadPtr(templateArray);       // Il2CppClass* sits at offset 0
        if (klass == 0)
        {
            error = "the existing array has no class pointer.";
            return 0;
        }

        nuint page = VirtualAllocEx(_process, 0, PageSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (page == 0)
        {
            error = "could not commit a scratch page in the game.";
            return 0;
        }

        bool finished = false;
        try
        {
            var stub = new X64Stub();
            stub.Prologue();
            stub.AttachThread(_exports["il2cpp_domain_get"], _exports["il2cpp_thread_attach"],
                page + ScratchThread);
            stub.CallNoArgs(_exports["il2cpp_gc_disable"]);
            stub.SetFlag(page + ScratchGcOff);          // past this point the collector is off
            stub.CallTwoArgs(_exports["il2cpp_array_new_specific"], klass, (ulong)length);
            stub.StoreRaxTo(page + ScratchResult);
            stub.DetachThread(_exports["il2cpp_thread_detach"], page + ScratchThread);
            stub.Epilogue();

            if (!Run(page, stub.ToArray(), out finished, out error))
            {
                // A stub that never started cannot have disabled anything, but one that timed
                // out may have got as far as il2cpp_gc_disable — and that is a counter, so
                // leaving it raised stops the game collecting for the rest of the session.
                // The marker says which happened; the page is leaked on timeout precisely so
                // it is still mapped and readable here.
                if (_mem.ReadI32(page + ScratchGcOff) != 0 && ResumeCollection(out _))
                    error += " The collector was re-enabled.";
                return 0;
            }

            nuint result = _mem.ReadPtr(page + ScratchResult);
            if (result == 0)
            {
                error = "the runtime refused the allocation.";
                ResumeCollection(out _);
            }
            return result;
        }
        finally
        {
            ReleasePage(page, finished);
        }
    }

    /// <summary>
    /// Re-enables the collector after <see cref="AllocateArrayLike"/>. Must be called once for
    /// each successful allocation, or the game's heap will grow unchecked for the rest of the
    /// session.
    /// </summary>
    public bool ResumeCollection(out string error)
    {
        nuint page = VirtualAllocEx(_process, 0, PageSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (page == 0)
        {
            error = "could not commit a scratch page to re-enable collection.";
            return false;
        }

        bool finished = false;
        try
        {
            var stub = new X64Stub();
            stub.Prologue();
            stub.AttachThread(_exports["il2cpp_domain_get"], _exports["il2cpp_thread_attach"],
                page + ScratchThread);
            stub.CallNoArgs(_exports["il2cpp_gc_enable"]);
            stub.DetachThread(_exports["il2cpp_thread_detach"], page + ScratchThread);
            stub.Epilogue();
            return Run(page, stub.ToArray(), out finished, out error);
        }
        finally
        {
            ReleasePage(page, finished);
        }
    }

    /// <summary>
    /// Frees the scratch page, but only once the thread that was running on it has finished.
    /// Releasing it under a still-running thread would unmap the code being executed, so a page
    /// is deliberately leaked instead — 4 KB for the rest of the session against a certain crash.
    /// </summary>
    private void ReleasePage(nuint page, bool threadFinished)
    {
        if (threadFinished) VirtualFreeEx(_process, page, 0, MEM_RELEASE);
    }

    /// <summary>Writes the stub into the scratch page and runs it once, waiting for it to finish.</summary>
    /// <param name="threadFinished">
    /// True when the injected thread ran to completion, so the page is safe to free. False both
    /// when the thread never started — nothing to wait for — and when it timed out.
    /// </param>
    private bool Run(nuint page, byte[] stub, out bool threadFinished, out string error)
    {
        error = "";
        threadFinished = true;    // nothing has been started yet, so nothing is using the page
        if (StubOffset + stub.Length > PageSize)
        {
            error = "the stub does not fit in the scratch page.";
            return false;
        }

        // Zero the scratch slots first so a stale value cannot be mistaken for a result.
        if (!_mem.Write(page, new byte[ScratchSize]) || !_mem.Write(page + StubOffset, stub))
        {
            error = "could not write the stub into the game.";
            return false;
        }

        IntPtr thread = CreateRemoteThread(_process, 0, 0, page + StubOffset, 0, 0, out _);
        if (thread == IntPtr.Zero)
        {
            error = "the game refused a new thread.";
            return false;
        }

        try
        {
            if (WaitForSingleObject(thread, CallTimeoutMs) != WAIT_OBJECT_0)
            {
                // Deliberately not killing the thread: terminating it mid-allocation would be
                // far more damaging than leaving it to finish on its own. Its page is leaked
                // for the same reason.
                threadFinished = false;
                error = "the injected call did not finish in time.";
                return false;
            }
            return true;
        }
        finally
        {
            CloseHandle(thread);
        }
    }

    /// <summary>
    /// Reads a loaded module's export table straight out of the target's memory, so the addresses
    /// are already relocated and no assumption is made about where the image landed.
    /// </summary>
    private static Dictionary<string, nuint> ReadExports(IMemorySource mem, nuint moduleBase)
    {
        var exports = new Dictionary<string, nuint>(StringComparer.Ordinal);

        int peOffset = mem.ReadI32(moduleBase + 0x3C);
        if (peOffset <= 0 || peOffset > 0x1000) throw new InvalidDataException("bad PE offset");
        nuint pe = moduleBase + (nuint)peOffset;
        if (mem.ReadI32(pe) != 0x4550) throw new InvalidDataException("no PE signature");   // "PE\0\0"

        nuint optional = pe + 24;
        int magic = mem.ReadI32(optional) & 0xFFFF;
        if (magic != 0x20B) throw new InvalidDataException("not a 64-bit image");

        int exportRva = mem.ReadI32(optional + 112);        // data directory 0
        if (exportRva == 0) throw new InvalidDataException("no export directory");
        nuint dir = moduleBase + (nuint)exportRva;

        int nameCount = mem.ReadI32(dir + 24);
        int functionsRva = mem.ReadI32(dir + 28);
        int namesRva = mem.ReadI32(dir + 32);
        int ordinalsRva = mem.ReadI32(dir + 36);
        if (nameCount <= 0 || nameCount > 65536) throw new InvalidDataException("implausible export count");

        for (int i = 0; i < nameCount; i++)
        {
            int nameRva = mem.ReadI32(moduleBase + (nuint)(namesRva + i * 4));
            if (nameRva == 0) continue;
            string name = mem.ReadNativeString(moduleBase + (nuint)nameRva, 128);
            if (name.Length == 0 || !name.StartsWith("il2cpp_", StringComparison.Ordinal)) continue;

            int ordinal = mem.ReadI32(moduleBase + (nuint)(ordinalsRva + i * 2)) & 0xFFFF;
            int functionRva = mem.ReadI32(moduleBase + (nuint)(functionsRva + ordinal * 4));
            if (functionRva != 0) exports[name] = moduleBase + (nuint)functionRva;
        }
        return exports;
    }

    public void Dispose() => _process.Dispose();

    // --- native ---------------------------------------------------------------------
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualAllocEx(SafeProcessHandle process, nuint address,
        nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFreeEx(SafeProcessHandle process, nuint address,
        nuint size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(SafeProcessHandle process,
        nuint attributes, nuint stackSize, nuint startAddress, nuint parameter, uint flags, out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>
/// Builds the small x86-64 stub that drives the runtime calls. Only the handful of instructions
/// needed are encoded, each as its own method, so the generated code stays readable next to the
/// bytes it produces.
///
/// <para>The stub is a <c>ThreadProc</c>: it ignores its argument, keeps the stack aligned for
/// the calls it makes, and returns 0.</para>
///
/// <para>Public so the verification harness can pin the exact bytes it emits. Nothing here can
/// be checked at run time — a mis-encoded instruction is a crash in someone else's process — so
/// the encoding is held against a known-good disassembly in the tests instead.</para>
/// </summary>
public sealed class X64Stub
{
    private readonly List<byte> _code = new(128);

    /// <summary>Shadow space for callees plus the 8 bytes that restore 16-byte alignment.</summary>
    public void Prologue() => Emit(0x48, 0x83, 0xEC, 0x28);            // sub rsp, 0x28

    public void Epilogue()
    {
        Emit(0x31, 0xC0);                                              // xor eax, eax
        Emit(0x48, 0x83, 0xC4, 0x28);                                  // add rsp, 0x28
        Emit(0xC3);                                                    // ret
    }

    /// <summary>Registers the injected thread with the runtime, saving the handle for the detach.</summary>
    public void AttachThread(nuint domainGet, nuint threadAttach, nuint threadSlot)
    {
        CallNoArgs(domainGet);
        Emit(0x48, 0x89, 0xC1);                                        // mov rcx, rax
        MovRax(threadAttach);
        Emit(0xFF, 0xD0);                                              // call rax
        StoreRaxTo(threadSlot);
    }

    public void DetachThread(nuint threadDetach, nuint threadSlot)
    {
        MovRax(threadSlot);
        Emit(0x48, 0x8B, 0x08);                                        // mov rcx, [rax]
        MovRax(threadDetach);
        Emit(0xFF, 0xD0);                                              // call rax
    }

    public void CallNoArgs(nuint function)
    {
        MovRax(function);
        Emit(0xFF, 0xD0);                                              // call rax
    }

    public void CallTwoArgs(nuint function, nuint first, ulong second)
    {
        Emit(0x48, 0xB9); EmitU64((ulong)first);                       // mov rcx, imm64
        Emit(0x48, 0xBA); EmitU64(second);                             // mov rdx, imm64
        MovRax(function);
        Emit(0xFF, 0xD0);                                              // call rax
    }

    /// <summary>Writes the return value of the last call into the scratch page.</summary>
    public void StoreRaxTo(nuint address)
    {
        Emit(0x48, 0xBA); EmitU64((ulong)address);                     // mov rdx, imm64
        Emit(0x48, 0x89, 0x02);                                        // mov [rdx], rax
    }

    /// <summary>
    /// Stamps 1 into the scratch page, so the trainer can tell how far a stub got when it
    /// stops answering. Clobbers rdx, which is only ever an argument register here and is
    /// always reloaded before the next call that wants it.
    /// </summary>
    public void SetFlag(nuint address)
    {
        Emit(0x48, 0xBA); EmitU64((ulong)address);                     // mov rdx, imm64
        Emit(0xC7, 0x02, 0x01, 0x00, 0x00, 0x00);                      // mov dword [rdx], 1
    }

    public byte[] ToArray() => _code.ToArray();

    private void MovRax(nuint value)
    {
        Emit(0x48, 0xB8); EmitU64((ulong)value);                       // mov rax, imm64
    }

    private void Emit(params byte[] bytes) => _code.AddRange(bytes);

    private void EmitU64(ulong value) => _code.AddRange(BitConverter.GetBytes(value));
}
