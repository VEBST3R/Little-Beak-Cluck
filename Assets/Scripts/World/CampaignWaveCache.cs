using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;
using System.Reflection;

namespace LittleBeakCluck.World
{
    [Serializable]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class CampaignWaveCacheFile
    {
        public string cacheId;
        public string cacheVersion;
        public int waveCount;
        public List<CampaignWaveCacheRecord> waves = new();
    }

    [Serializable]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class CampaignWaveCacheRecord
    {
        public string name;
        public string requiredPlayerWave;
        public List<CampaignWaveCacheEntry> entries = new();
        public int rewardCoins;
    }

    [Serializable]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class CampaignWaveCacheEntry
    {
        public int prefabIndex;
        public int count;
        public string spawnPointKey;
        public int fallbackIndex;
        public float spawnInterval;
    }

    public static class CampaignWaveCacheStorage
    {
        private const string FilePrefix = "campaign_waves_";
        private const string FileExtension = ".json";

        public static CampaignWaveCacheFile Load(string cacheId)
        {
            try
            {
                string path = GetCachePath(cacheId);
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonUtility.FromJson<CampaignWaveCacheFile>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampaignWaveCacheStorage] Failed to load cache '{cacheId}': {ex.Message}");
                return null;
            }
        }

        public static void Save(string cacheId, CampaignWaveCacheFile data)
        {
            if (data == null)
                return;

            try
            {
                string path = GetCachePath(cacheId);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, prettyPrint: false);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampaignWaveCacheStorage] Failed to save cache '{cacheId}': {ex.Message}");
            }
        }

        public static void Delete(string cacheId)
        {
            try
            {
                string path = GetCachePath(cacheId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampaignWaveCacheStorage] Failed to delete cache '{cacheId}': {ex.Message}");
            }
        }

        private static string GetCachePath(string cacheId)
        {
            if (string.IsNullOrWhiteSpace(cacheId))
                cacheId = "default";

            return Path.Combine(Application.persistentDataPath, FilePrefix + cacheId + FileExtension);
        }
    }
}
