using System;
using System.Collections;
using System.Collections.Generic;
using Minecraft.Assets;
using Minecraft.Lua;
using Newtonsoft.Json;
using UnityEngine;

namespace Minecraft.Configurations
{
    [CreateAssetMenu(menuName = "Minecraft/Configurations/BiomeTable")]
    public class BiomeTable : ScriptableObject, ILuaCallCSharp
    {
        [SerializeField][EnsureAssetType(typeof(TextAsset))] private AssetPtr m_BiomeTableJson;

        [NonSerialized] private BiomeData[] m_Biomes;
        [NonSerialized] private Dictionary<string, BiomeData> m_BiomeMap;

        public IEnumerator Initialize()
        {
            yield return InitBiomes();
        }

        private IEnumerator InitBiomes()
        {
            // 这个ScriptableObject本身只存储的对应table的GUID, 这里才正式加载进来
            AsyncAsset json = AssetManager.Instance.LoadAsset<TextAsset>(m_BiomeTableJson);
            yield return json;

            // json解密成BiomeData[]类型
            m_Biomes = JsonConvert.DeserializeObject<BiomeData[]>(json.GetAssetAs<TextAsset>().text);
            AssetManager.Instance.UnloadAsset(json);

            // 数据装载进map
            m_BiomeMap = new Dictionary<string, BiomeData>(m_Biomes.Length);
            for (int i = 0; i < m_Biomes.Length; i++)
            {
                BiomeData biome = m_Biomes[i];
                m_BiomeMap.Add(biome.InternalName, biome);
            }
        }


        public BiomeData GetBiome(int id)
        {
            return m_Biomes[id];
        }

        public BiomeData GetBiome(string name)
        {
            return m_BiomeMap[name];
        }
    }
}
