using System.Globalization;

namespace ULTRAKILLSplitScreen.Launcher;

internal static class LayoutEngine
{
    public static PlayerWindow[] Create(LauncherConfig config)
    {
        int screenWidth = NativeWindow.ScreenWidth;
        int screenHeight = NativeWindow.ScreenHeight;

        if (screenWidth <= 0 || screenHeight <= 0)
            throw new InvalidOperationException("Windows returned an invalid primary-screen resolution.");

        WindowArea screen = new(0, 0, screenWidth, screenHeight);
        WindowArea[] tiles = CreateTiles(screen, config.Players, ResolveLayout(config.Layout, config.Players));
        double aspectRatio = TryParseAspectRatio(config.TargetAspectRatio, out double parsed) ? parsed : 16d / 9d;

        var result = new PlayerWindow[config.Players];
        for (int index = 0; index < config.Players; index++)
        {
            WindowArea tile = Inset(tiles[index], config.WindowGapPixels);
            WindowArea content = string.Equals(config.AspectMode, "fit", StringComparison.OrdinalIgnoreCase)
                ? FitAspect(tile, aspectRatio)
                : tile;
            result[index] = new PlayerWindow(index + 1, tile, content);
        }

        return result;
    }

    public static bool TryParseAspectRatio(string? value, out double ratio)
    {
        ratio = 0d;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        string[] parts = trimmed.Split(':', '/', 'x', 'X');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
            && width > 0d
            && height > 0d)
        {
            ratio = width / height;
            return ratio is >= 0.5d and <= 5d;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double direct)
            && direct is >= 0.5d and <= 5d)
        {
            ratio = direct;
            return true;
        }

        return false;
    }

    private static string ResolveLayout(string requested, int players)
    {
        if (!string.Equals(requested, "auto", StringComparison.OrdinalIgnoreCase))
            return requested;

        return players switch
        {
            1 => "grid",
            2 => "vertical",
            _ => "grid"
        };
    }

    private static WindowArea[] CreateTiles(WindowArea screen, int players, string layout)
    {
        return layout switch
        {
            "vertical" => CreateColumns(screen, players),
            "horizontal" => CreateRows(screen, players),
            _ => CreateGrid(screen, players)
        };
    }

    private static WindowArea[] CreateColumns(WindowArea screen, int players)
    {
        var areas = new WindowArea[players];
        for (int index = 0; index < players; index++)
        {
            int startX = screen.X + screen.Width * index / players;
            int endX = screen.X + screen.Width * (index + 1) / players;
            areas[index] = new WindowArea(startX, screen.Y, endX - startX, screen.Height);
        }

        return areas;
    }

    private static WindowArea[] CreateRows(WindowArea screen, int players)
    {
        var areas = new WindowArea[players];
        for (int index = 0; index < players; index++)
        {
            int startY = screen.Y + screen.Height * index / players;
            int endY = screen.Y + screen.Height * (index + 1) / players;
            areas[index] = new WindowArea(screen.X, startY, screen.Width, endY - startY);
        }

        return areas;
    }

    private static WindowArea[] CreateGrid(WindowArea screen, int players)
    {
        if (players == 1)
            return [screen];
        if (players == 2)
            return CreateColumns(screen, players);

        int leftWidth = screen.Width / 2;
        int topHeight = screen.Height / 2;
        WindowArea[] grid =
        [
            new(screen.X, screen.Y, leftWidth, topHeight),
            new(screen.X + leftWidth, screen.Y, screen.Width - leftWidth, topHeight),
            new(screen.X, screen.Y + topHeight, leftWidth, screen.Height - topHeight),
            new(screen.X + leftWidth, screen.Y + topHeight, screen.Width - leftWidth, screen.Height - topHeight)
        ];
        return grid.Take(players).ToArray();
    }

    private static WindowArea Inset(WindowArea area, int gap)
    {
        int inset = Math.Max(0, gap / 2);
        int width = Math.Max(320, area.Width - inset * 2);
        int height = Math.Max(240, area.Height - inset * 2);
        return new WindowArea(area.X + inset, area.Y + inset, width, height);
    }

    private static WindowArea FitAspect(WindowArea area, double targetRatio)
    {
        double currentRatio = area.Width / (double)area.Height;
        int width;
        int height;

        if (currentRatio > targetRatio)
        {
            height = area.Height;
            width = Math.Max(320, (int)Math.Round(height * targetRatio));
        }
        else
        {
            width = area.Width;
            height = Math.Max(240, (int)Math.Round(width / targetRatio));
        }

        width = Math.Min(width, area.Width);
        height = Math.Min(height, area.Height);
        int x = area.X + (area.Width - width) / 2;
        int y = area.Y + (area.Height - height) / 2;
        return new WindowArea(x, y, width, height);
    }
}
