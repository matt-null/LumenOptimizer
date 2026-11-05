using LumenOptimizer.Core.Engine;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.UI;

/// <summary>Shared singletons: the tweak catalog, backup store and engine.</summary>
public static class AppServices
{
    public static BackupStore Backups { get; }
    public static TweakEngine Engine { get; }
    public static IReadOnlyList<Tweak> Catalog { get; }
    public static IReadOnlyList<string> CatalogProblems { get; }

    static AppServices()
    {
        Backups = new BackupStore();
        Engine = new TweakEngine(Backups);

        try
        {
            Catalog = CatalogLoader.LoadEmbedded();
            CatalogProblems = CatalogLoader.Validate(Catalog);
            foreach (var p in CatalogProblems) Log.Warn("Catalog: " + p);
        }
        catch (Exception ex)
        {
            Log.Error("Could not load catalog: " + ex);
            Catalog = Array.Empty<Tweak>();
            CatalogProblems = new[] { "Could not load the tweak catalog: " + ex.Message };
        }
    }
}
