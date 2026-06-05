using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GersangTracker.Services
{
    public class ItemInfo
    {
        public string name { get; set; }
    }

    public class ItemDatabaseService
    {
        private Dictionary<string, string> _itemDatabase = new();

        public ItemDatabaseService()
        {
            LoadDatabase();
        }

        public void LoadDatabase()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "GersangTracker.Data.GersangItems.json";

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        var db = JsonSerializer.Deserialize<Dictionary<string, ItemInfo>>(json);
                        if (db != null)
                        {
                            foreach (var kvp in db)
                            {
                                _itemDatabase[kvp.Key] = kvp.Value.name;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading GersangItems.json: {ex.Message}");
            }
        }

        public string GetItemName(string itemId)
        {
            if (_itemDatabase.TryGetValue(itemId, out string name))
            {
                return name;
            }
            return "";
        }
    }
}
