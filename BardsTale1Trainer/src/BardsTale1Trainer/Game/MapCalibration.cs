namespace BardsTale1Trainer.Game;

/// <summary>One calibration anchor: "game cell (GameX, GameY) is drawn at pixel (PixelX, PixelY)".</summary>
public sealed record MapAnchor(double PixelX, double PixelY, int GameX, int GameY);

/// <summary>
/// Maps between game coordinates and map-image pixels with an axis-aligned linear transform
/// derived from two anchors. The trainer can't know which way the game's axes run on a given
/// map (or even which adjacent byte is X), so the transform isn't hardcoded; instead the user
/// marks the party's spot twice (standing at two different positions) and the scale/origin
/// fall out. A negative Y scale — the game's North axis growing upward while image pixels
/// grow downward — is handled by the math without special-casing.
/// </summary>
public sealed class MapCalibration
{
    private readonly double _scaleX, _scaleY;   // pixels per game cell
    private readonly double _originX, _originY; // pixel position of game (0,0)

    private MapCalibration(double scaleX, double originX, double scaleY, double originY)
    {
        _scaleX = scaleX; _originX = originX;
        _scaleY = scaleY; _originY = originY;
    }

    /// <summary>
    /// Builds the transform from two anchors, or returns null when they can't define one
    /// (the anchors must differ in both game X and game Y — i.e. the two marked positions
    /// must not share a row or a column).
    /// </summary>
    public static MapCalibration? FromAnchors(MapAnchor a, MapAnchor b)
    {
        if (a.GameX == b.GameX || a.GameY == b.GameY) return null;
        double scaleX = (b.PixelX - a.PixelX) / (b.GameX - a.GameX);
        double scaleY = (b.PixelY - a.PixelY) / (b.GameY - a.GameY);
        if (scaleX == 0 || scaleY == 0) return null;   // distinct cells drawn at the same pixel: not a usable transform
        return new MapCalibration(scaleX, a.PixelX - scaleX * a.GameX,
                                  scaleY, a.PixelY - scaleY * a.GameY);
    }

    /// <summary>Pixel position of the centre of game cell (<paramref name="gameX"/>, <paramref name="gameY"/>).</summary>
    public (double X, double Y) ToPixel(int gameX, int gameY) =>
        (_originX + _scaleX * gameX, _originY + _scaleY * gameY);

    /// <summary>The game cell whose centre is nearest to the given pixel.</summary>
    public (int X, int Y) ToGame(double pixelX, double pixelY) =>
        ((int)Math.Round((pixelX - _originX) / _scaleX),
         (int)Math.Round((pixelY - _originY) / _scaleY));
}
