using System;
using System.Collections;
using System.Collections.Generic;
using Minecraft.Assets;
using Minecraft.Lua;
using Newtonsoft.Json;
using UnityEngine;
using XLua;

namespace Minecraft.Configurations
{
    [CreateAssetMenu(menuName = "Minecraft/Configurations/BlockTable")]
    public class BlockTable : ScriptableObject, IDisposable, ILuaCallCSharp
    {
        [SerializeField][EnsureAssetType(typeof(TextAsset))] private AssetPtr m_BlockTableJson;
        [SerializeField][EnsureAssetType(typeof(TextAsset))] private AssetPtr m_BlockMeshTableJson;
        [SerializeField][EnsureAssetType(typeof(TextAsset))] private AssetPtr m_BlockTextureTableJson;
        [SerializeField][EnsureAssetType(typeof(TextAsset))] private AssetPtr m_BlockMaterialTableJson;

        [NonSerialized] private BlockData[] m_Blocks;
        [NonSerialized] private Dictionary<string, BlockData> m_BlockMap;
        [NonSerialized] private IBlockBehaviour[] m_BlockBehaviors;
        [NonSerialized] private AssetPtr[] m_BlockMeshPtrs;
        [NonSerialized] private BlockMesh[] m_BlockMeshes;
        [NonSerialized] private Texture2DArray m_TextureArray;
        [NonSerialized] private AssetPtr[] m_MaterialPtrs;
        [NonSerialized] private Material[] m_Materials;

        public int BlockCount => m_Blocks.Length;

        public int MaterialCount => m_Materials.Length;

        /// <summary>
        /// 初始化资源
        /// </summary>
        /// <returns></returns>
        public IEnumerator Initialize()
        {
            yield return InitBlocks();
            yield return InitBlockMeshes();
            yield return InitTextures();
            yield return InitMaterials();

            // 各个table在加载之后都不需要了
            AssetManager.Instance.UnloadAsset(m_BlockTableJson);
            AssetManager.Instance.UnloadAsset(m_BlockMeshTableJson);
            AssetManager.Instance.UnloadAsset(m_BlockTextureTableJson);
            AssetManager.Instance.UnloadAsset(m_BlockMaterialTableJson);
        }

        /// <summary>
        /// 把lua里面定义的行为链接到block, 并调用init函数
        /// </summary>
        /// <param name="world"></param>
        public void LoadBlockBehavioursInLua(IWorld world)
        {
            m_BlockBehaviors = new IBlockBehaviour[m_Blocks.Length];

            for (int i = 0; i < m_Blocks.Length; i++)
            {
                ref IBlockBehaviour behaviour = ref m_BlockBehaviors[i];
                behaviour = world.LuaManager.GetLuaGlobal<IBlockBehaviour>(m_Blocks[i].InternalName);
                if (behaviour != null)
                {
                    behaviour?.init(world, m_Blocks[i]);
                    Debug.Log($"LoadBlockBehavioursInLua() behaviour: {m_Blocks[i].InternalName}");
                }
            }
        }

        public void Dispose()
        {
            m_Blocks = null;
            m_BlockMap = null;
            m_BlockBehaviors = null;
            m_BlockMeshes = null;
            m_TextureArray = null;
            m_Materials = null;

            AssetManager.Instance.UnloadAssets(m_BlockMeshPtrs);
            AssetManager.Instance.UnloadAssets(m_MaterialPtrs);
        }


        private IEnumerator InitBlocks()
        {
            // 这个ScriptableObject本身只存储的对应table的GUID, 这里才正式加载进来
            AsyncAsset json = AssetManager.Instance.LoadAsset<TextAsset>(m_BlockTableJson);
            yield return json;

            // json解密成BlockData[]类型
            m_Blocks = JsonConvert.DeserializeObject<BlockData[]>(json.GetAssetAs<TextAsset>().text);

            // 把数据装填进map
            m_BlockMap = new Dictionary<string, BlockData>(m_Blocks.Length);
            for (int i = 0; i < m_Blocks.Length; i++)
            {
                BlockData block = m_Blocks[i];
                m_BlockMap.Add(block.InternalName, block);
            }
        }

        private IEnumerator InitBlockMeshes()
        {
            // 这个ScriptableObject本身只存储的对应table的GUID, 这里才正式加载进来
            AsyncAsset json = AssetManager.Instance.LoadAsset<TextAsset>(m_BlockMeshTableJson);
            yield return json;

            // json解密成AssetPtr[]类型
            m_BlockMeshPtrs = JsonConvert.DeserializeObject<AssetPtr[]>(json.GetAssetAs<TextAsset>().text);
            m_BlockMeshes = new BlockMesh[m_BlockMeshPtrs.Length];

            // 加载BlockMesh[]
            AsyncAsset[] meshes = AssetManager.Instance.LoadAssets<BlockMesh>(m_BlockMeshPtrs);
            // 等待所有meshes都加载完毕, 然后再装载进入m_BlockMeshes中
            yield return AsyncAsset.WaitAll(m_BlockMeshes, meshes);
        }

        private IEnumerator InitTextures()
        {
            // 这个ScriptableObject本身只存储的对应table的GUID, 这里才正式加载进来
            AsyncAsset json = AssetManager.Instance.LoadAsset<TextAsset>(m_BlockTextureTableJson);
            yield return json;

            // json解密成AssetPtr[]类型
            AssetPtr[] m_TexturePtrs = JsonConvert.DeserializeObject<AssetPtr[]>(json.GetAssetAs<TextAsset>().text);

            // 加载第一个图, 用来创建texArray
            AsyncAsset firstTexAsset = AssetManager.Instance.LoadAsset<Texture2D>(m_TexturePtrs[0]);
            yield return firstTexAsset;

            Texture2D firstTex = firstTexAsset.GetAssetAs<Texture2D>();

            m_TextureArray = new Texture2DArray(firstTex.width, firstTex.height, m_TexturePtrs.Length, firstTex.format, false)
            {
                anisoLevel = firstTex.anisoLevel,
                mipMapBias = firstTex.mipMapBias,
                wrapMode = firstTex.wrapMode,
                filterMode = firstTex.filterMode
            };

            // 把图片都塞进texArray
            for (int i = 0; i < m_TexturePtrs.Length; i++)
            {
                AsyncAsset texture = AssetManager.Instance.LoadAsset<Texture2D>(m_TexturePtrs[i]);
                yield return texture;

                Graphics.CopyTexture(texture.GetAssetAs<Texture2D>(), 0, 0, m_TextureArray, i, 0);
            }

            // m_TexturePtrs用不到了, 直接释放掉
            AssetManager.Instance.UnloadAssets(m_TexturePtrs);
            m_TexturePtrs = null;
        }

        private IEnumerator InitMaterials()
        {
            // 这个ScriptableObject本身只存储的对应table的GUID, 这里才正式加载进来
            AsyncAsset json = AssetManager.Instance.LoadAsset<TextAsset>(m_BlockMaterialTableJson);
            yield return json;

            // json解密成AssetPtr[]类型
            m_MaterialPtrs = JsonConvert.DeserializeObject<AssetPtr[]>(json.GetAssetAs<TextAsset>().text);
            m_Materials = new Material[m_MaterialPtrs.Length];

            // 加载材质球
            AsyncAsset[] materials = AssetManager.Instance.LoadAssets<Material>(m_MaterialPtrs);
            yield return AsyncAsset.WaitAll(m_Materials, materials);
        }


        public BlockData GetBlock(int id)
        {
            return m_Blocks[id];
        }

        public BlockData GetBlock(string internalName)
        {
            return m_BlockMap[internalName];
        }

        public IBlockBehaviour GetBlockBehaviour(int id)
        {
            return m_BlockBehaviors[id];
        }

        public BlockMesh GetMesh(int index)
        {
            return m_BlockMeshes[index];
        }

        public Material GetMaterial(int index)
        {
            return m_Materials[index];
        }

        public Texture2DArray GetTextureArray()
        {
            return m_TextureArray;
        }
    }
}
