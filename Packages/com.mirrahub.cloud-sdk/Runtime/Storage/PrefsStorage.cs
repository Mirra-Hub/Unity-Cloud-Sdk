using System;
using UnityEngine;

namespace MirraCloud.Core.Storage
{
    public class PrefsStorage : IStorage
    {
        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public string GetString(string key)
        {
            return PlayerPrefs.GetString(key);
        }

        public void SaveString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public void DeleteKeys(params string[] keys)
        {
            foreach (var key in keys)
            {
                PlayerPrefs.DeleteKey(key);
            }
            
            PlayerPrefs.Save();
        }
    }
}