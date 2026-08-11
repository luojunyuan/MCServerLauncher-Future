namespace MCServerLauncher.WinUI.InstanceConsole.Modules;

/// <summary>
/// Single source of truth for normalizing daemon virtual paths (e.g.
/// <c>/instances/{id}/mods</c>). Resolves <c>.</c> and <c>..</c> segments so every
/// console page agrees on the result.
/// </summary>
public static class VirtualPath
{
    public static string Normalize(string path)
    {
        var stack = new Stack<string>();
        foreach (var part in (path ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (stack.Count > 0) stack.Pop(); continue; }
            stack.Push(part);
        }

        if (stack.Count == 0) return "/";
        var values = stack.ToArray();
        Array.Reverse(values);
        return "/" + string.Join('/', values);
    }
}
