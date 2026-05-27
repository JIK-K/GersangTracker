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
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "GersangItems.json");
            
            // For development time fallback if BaseDirectory doesn't have it
            if (!File.Exists(dbPath)) 
            {
                dbPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "GersangItems.json");
            }

            if (File.Exists(dbPath))
            {
                try
                {
                    string json = File.ReadAllText(dbPath);
                    var db = JsonSerializer.Deserialize<Dictionary<string, ItemInfo>>(json);
                    if (db != null)
                    {
                        foreach (var kvp in db)
                        {
                            _itemDatabase[kvp.Key] = kvp.Value.name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading GersangItems.json: {ex.Message}");
                }
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
