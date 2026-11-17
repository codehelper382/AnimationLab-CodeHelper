using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Local grouping only; never changes a take's portable format or an existing asset.
    internal static class LabTakeCatalog
    {
        [Serializable] private sealed class Entry { public string guid, lineage; }
        [Serializable] private sealed class Catalog { public List<Entry> entries = new List<Entry>(); }
        private static string PathName => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "AnimationLabVersions.json"));
        private static Catalog Read()
        {
            try { return File.Exists(PathName) ? JsonUtility.FromJson<Catalog>(File.ReadAllText(PathName)) ?? new Catalog() : new Catalog(); }
            catch (Exception) { return new Catalog(); }
        }
        internal static string Lineage(AnimationLabTake take)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(take));
            return Read().entries.FirstOrDefault(e => e.guid == guid)?.lineage
                ?? (string.IsNullOrEmpty(guid) ? Guid.NewGuid().ToString("N") : guid);
        }
        internal static void Register(AnimationLabTake take, string lineage)
        {
            var catalog = Read();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(take));
            catalog.entries.RemoveAll(e => e.guid == guid);
            catalog.entries.Add(new Entry { guid = guid, lineage = lineage });
            string temporary = PathName + ".writing";
            File.WriteAllText(temporary, JsonUtility.ToJson(catalog, true));
            if (File.Exists(PathName)) File.Replace(temporary, PathName, null);
            else File.Move(temporary, PathName);
        }
    }
}
