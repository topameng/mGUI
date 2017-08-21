//----------------------------------------------
//            NGUI: Next-Gen UI kit
// Copyright © 2011-2016 Tasharen Entertainment
//----------------------------------------------
#define SHOW_HIDDEN_OBJECTS
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// This is an internally-created script used by the UI system. You shouldn't be attaching it manually.
/// </summary>

[ExecuteInEditMode]
[AddComponentMenu("NGUI/Internal/Draw Call")]
public class UIDrawCall : MonoBehaviour
{
	static BetterList<UIDrawCall> mActiveList = new BetterList<UIDrawCall>();
	static BetterList<UIDrawCall> mInactiveList = new BetterList<UIDrawCall>();

	[System.Obsolete("Use UIDrawCall.activeList")]
	static public BetterList<UIDrawCall> list { get { return mActiveList; } }

	/// <summary>
	/// List of active draw calls.
	/// </summary>

	static public BetterList<UIDrawCall> activeList { get { return mActiveList; } }

	/// <summary>
	/// List of inactive draw calls. Only used at run-time in order to avoid object creation/destruction.
	/// </summary>

	static public BetterList<UIDrawCall> inactiveList { get { return mInactiveList; } }

	public enum Clipping : int
	{
		None = 0,
		TextureMask = 1,			// Clipped using a texture rather than math
		SoftClip = 3,				// Alpha-based clipping with a softened edge
		ConstrainButDontClip = 4,	// No actual clipping, but does have an area
	}

    public enum BatchMode
    {
        Static,
        Single,
        Dynamic,
    }

    [HideInInspector][System.NonSerialized] public int widgetCount = 0;
	[HideInInspector][System.NonSerialized] public int depthStart = int.MaxValue;
	[HideInInspector][System.NonSerialized] public int depthEnd = int.MinValue;
	[HideInInspector][System.NonSerialized] public UIPanel manager;
	[HideInInspector][System.NonSerialized] public UIPanel panel;
	[HideInInspector][System.NonSerialized] public Texture2D clipTexture;
	[HideInInspector][System.NonSerialized] public bool alwaysOnScreen = false;
#if NGUI_BACKUP
    [HideInInspector][System.NonSerialized] public List<Vector3> verts = new List<Vector3>();
	[HideInInspector][System.NonSerialized] public List<Vector3> norms = new List<Vector3>();
	[HideInInspector][System.NonSerialized] public List<Vector4> tans = new List<Vector4>();
	[HideInInspector][System.NonSerialized] public List<Vector2> uvs = new List<Vector2>();
	[HideInInspector][System.NonSerialized] public List<Vector4> uv2 = new List<Vector4>();
	[HideInInspector][System.NonSerialized] public List<Color> cols = new List<Color>();    
#else
    [HideInInspector][System.NonSerialized] public FreeList<Vector3> verts = new FreeList<Vector3>();
    [HideInInspector][System.NonSerialized] public FreeList<Vector3> norms = new FreeList<Vector3>();
    [HideInInspector][System.NonSerialized] public FreeList<Vector4> tans = new FreeList<Vector4>();
    [HideInInspector][System.NonSerialized] public FreeList<Vector2> uvs = new FreeList<Vector2>();
    [HideInInspector][System.NonSerialized] public FreeList<Vector2> uv2 = new FreeList<Vector2>();
    [HideInInspector][System.NonSerialized] public FreeList<Color> cols = new FreeList<Color>();
#endif

    [System.NonSerialized] Material		mMaterial;		// Material used by this draw call
	[System.NonSerialized] Texture		mTexture;		// Main texture used by the material
	[System.NonSerialized] Shader		mShader;		// Shader used by the dynamically created material
	[System.NonSerialized] int			mClipCount = 0;	// Number of times the draw call's content is getting clipped
	[System.NonSerialized] Transform	mTrans;			// Cached transform
	[System.NonSerialized] Mesh			mMesh;			// First generated mesh
	[System.NonSerialized] MeshFilter	mFilter;		// Mesh filter for this draw call
	[System.NonSerialized] MeshRenderer	mRenderer;		// Mesh renderer for this screen	
#if NGUI_BACKUP
    [System.NonSerialized] Material		mDynamicMat;    // Instantiated material    
#else
    [System.NonSerialized] DynamicMaterial mDynamicMat;    // Instantiated material   
#endif

#if NGUI_BACKUP || UNITY_4_7
    [System.NonSerialized] int[]		mIndices;		// Cached indices    
#else
    [System.NonSerialized] List<int>    mIndices = new List<int>();
#endif

#if UNITY_4_7
	[System.NonSerialized] Vector3[] mTempVerts = null;
	[System.NonSerialized] Vector2[] mTempUV0 = null;
	[System.NonSerialized] Vector2[] mTempUV2 = null;
	[System.NonSerialized] Color[] mTempCols = null;
	[System.NonSerialized] Vector3[] mTempNormals = null;
	[System.NonSerialized] Vector4[] mTempTans = null;
#else
    [System.NonSerialized] ShadowMode mShadowMode = ShadowMode.None;
#endif
	[System.NonSerialized] bool mRebuildMat = true;
	[System.NonSerialized] bool mLegacyShader = false;
	[System.NonSerialized] int mRenderQueue = 3000;
	[System.NonSerialized] int mTriangles = 0;

	/// <summary>
	/// Whether the draw call has changed recently.
	/// </summary>

	[System.NonSerialized]
	public bool isDirty = false;

	[System.NonSerialized] bool mTextureClip = false;
	[System.NonSerialized] bool mIsNew = true;

	/// <summary>
	/// Callback that will be triggered at OnWillRenderObject() time.
	/// </summary>

	public OnRenderCallback onRender;
	public delegate void OnRenderCallback (Material mat);

	/// <summary>
	/// Callback that will be triggered when a new draw call gets created.
	/// </summary>

	public OnCreateDrawCall onCreateDrawCall; 
	public delegate void OnCreateDrawCall (UIDrawCall dc, MeshFilter filter, MeshRenderer ren);

	/// <summary>
	/// Render queue used by the draw call.
	/// </summary>

	public int renderQueue
	{
		get
		{
			return mRenderQueue;
		}
		set
		{
			if (mRenderQueue != value)
			{
				mRenderQueue = value;

				if (mDynamicMat != null)
				{
					mDynamicMat.renderQueue = value;
#if UNITY_EDITOR
					if (mRenderer != null && !Application.isPlaying) mRenderer.enabled = isActive;
#endif
				}
			}
		}
	}

	/// <summary>
	/// Renderer's sorting order, to be used with Unity's 2D system.
	/// </summary>

	public int sortingOrder
	{
		get
		{
			return mSortingOrder;
		}
		set
		{
			if (mSortingOrder != value)
			{
				mSortingOrder = value;
				if (mRenderer != null) mRenderer.sortingOrder = value;
			}
		}
	}

	/// <summary>
	/// Renderer's sorting layer name, used with the Unity's 2D system.
	/// </summary>

	public string sortingLayerName
	{
		get
		{
			if (!string.IsNullOrEmpty(mSortingLayerName)) return mSortingLayerName;
			if (mRenderer == null) return null;
			mSortingLayerName = mRenderer.sortingLayerName;
			return mSortingLayerName;
		}
		set
		{
			if (mRenderer != null && mSortingLayerName != value)
			{
				mSortingLayerName = value;
				mRenderer.sortingLayerName = value;
			}
		}
	}

	[System.NonSerialized] string mSortingLayerName;
	[System.NonSerialized] int mSortingOrder = 0;

	/// <summary>
	/// Final render queue used to draw the draw call's geometry.
	/// </summary>

	public int finalRenderQueue
	{
		get
		{
			return (mDynamicMat != null) ? mDynamicMat.renderQueue : mRenderQueue;
		}
	}

#if UNITY_EDITOR

	/// <summary>
	/// Whether the draw call is currently active.
	/// </summary>

	public bool isActive
	{
		get
		{
			return mActive;
		}
		set
		{
			if (mActive != value)
			{
				mActive = value;

				if (mRenderer != null)
				{
					mRenderer.enabled = value;
					NGUITools.SetDirty(gameObject);
				}
			}
		}
	}
	bool mActive = true;
#endif

    /// <summary>
    /// Transform is cached for speed and efficiency.
    /// </summary>

#if UNITY_EDITOR
    public Transform cachedTransform { get { if (mTrans == null) mTrans = transform; return mTrans; } }
#else
    public Transform cachedTransform { get { return mTrans; } }
#endif

    /// <summary>
    /// Material used by this screen.
    /// </summary>

    public Material baseMaterial
	{
		get
		{
			return mMaterial;
		}
		set
		{
			if (mMaterial != value)
			{
				mMaterial = value;
				mRebuildMat = true;
			}
		}
	}

	/// <summary>
	/// Dynamically created material used by the draw call to actually draw the geometry.
	/// </summary>

	public Material dynamicMaterial { get { return mDynamicMat; } }

	/// <summary>
	/// Texture used by the material.
	/// </summary>

	public Texture mainTexture
	{
		get
		{
			return mTexture;
		}
		set
		{
			mTexture = value;
			if (mBlock == null) mBlock = new MaterialPropertyBlock();
			mBlock.SetTexture("_MainTex", value);
		}
	}

	/// <summary>
	/// Shader used by the material.
	/// </summary>

	public Shader shader
	{
		get
		{
			return mShader;
		}
		set
		{
			if (mShader != value)
			{
				mShader = value;
				mRebuildMat = true;
			}
		}
	}

#if !NGUI_BACKUP
    public Renderer Renderer
    {
        get
        {
            return mRenderer;
        }
    }
#endif

#if !UNITY_4_7
    public enum ShadowMode
	{
		None,
		Receive,
		CastAndReceive,
	}

	/// <summary>
	/// Shadow casting method.
	/// </summary>

	public ShadowMode shadowMode
	{
		get
		{
			return mShadowMode;
		}
		set
		{
			if (mShadowMode != value)
			{
				mShadowMode = value;

				if (mRenderer != null)
				{
					if (mShadowMode == ShadowMode.None)
					{
						mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
						mRenderer.receiveShadows = false;
					}
					else if (mShadowMode == ShadowMode.Receive)
					{
						mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
						mRenderer.receiveShadows = true;
					}
					else
					{
						mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
						mRenderer.receiveShadows = true;
					}
				}
			}
		}
	}
#endif

	/// <summary>
	/// The number of triangles in this draw call.
	/// </summary>

	public int triangles { get { return (mMesh != null) ? mTriangles : 0; } }

	/// <summary>
	/// Whether the draw call is currently using a clipped shader.
	/// </summary>

	public bool isClipped { get { return mClipCount != 0; } }

    public BatchMode batch
    {
        get;
        set;
    }

    private float rootScale = 1;

	/// <summary>
	/// Create an appropriate material for the draw call.
	/// </summary>

	void CreateMaterial ()
	{
		mTextureClip = false;
		mLegacyShader = false;
		mClipCount = panel.clipCount;

		string shaderName = (mShader != null) ? mShader.name :
			((mMaterial != null) ? mMaterial.shader.name : "Unlit/Transparent Colored");

		// Figure out the normal shader's name
		shaderName = shaderName.Replace("GUI/Text Shader", "Unlit/Text");

		if (shaderName.Length > 2)
		{
			if (shaderName[shaderName.Length - 2] == ' ')
			{
				int index = shaderName[shaderName.Length - 1];
				if (index > '0' && index <= '9') shaderName = shaderName.Substring(0, shaderName.Length - 2);
			}
		}

		if (shaderName.StartsWith("Hidden/"))
			shaderName = shaderName.Substring(7);

		// Legacy functionality
		const string soft = " (SoftClip)";
		shaderName = shaderName.Replace(soft, "");

		const string textureClip = " (TextureClip)";
		shaderName = shaderName.Replace(textureClip, "");

        if (panel != null && panel.clipping == Clipping.TextureMask)
        {
            mTextureClip = true;
            shader = Shader.Find("Hidden/" + shaderName + textureClip);
        }
        else if (mClipCount != 0)
        {
            shader = Shader.Find("Hidden/" + shaderName + " " + mClipCount);
            if (shader == null) shader = Shader.Find(shaderName + " " + mClipCount);

            // Legacy functionality
            if (shader == null && mClipCount == 1)
            {
                mLegacyShader = true;
                shader = Shader.Find(shaderName + soft);
            }
        }
        else
        {
            shader = Shader.Find(shaderName);
        }

		// Always fallback to the default shader
		if (shader == null) shader = Shader.Find("Unlit/Transparent Colored");

		if (mMaterial != null)
		{
#if NGUI_BACKUP
            mDynamicMat = new Material(mMaterial);
#else
            mDynamicMat = new DynamicMaterial(mMaterial);
#endif
            CalMaterialOffset();
            mDynamicMat.name = "[NGUI] " + mMaterial.name;
			mDynamicMat.hideFlags = (HideFlags.DontSave | HideFlags.NotEditable);
			mDynamicMat.CopyPropertiesFromMaterial(mMaterial);
#if !UNITY_FLASH
			string[] keywords = mMaterial.shaderKeywords;
			for (int i = 0; i < keywords.Length; ++i)
				mDynamicMat.EnableKeyword(keywords[i]);
#endif
			// If there is a valid shader, assign it to the custom material
			if (shader != null)
			{
				mDynamicMat.shader = shader;
			}
			else if (mClipCount != 0)
			{
				Debug.LogError(shaderName + " shader doesn't have a clipped shader version for " + mClipCount + " clip regions");
			}
		}
		else
		{
#if NGUI_BACKUP
            mDynamicMat = new Material(shader);
#else
            mDynamicMat = new DynamicMaterial(shader);
#endif
            CalMaterialOffset();
            mDynamicMat.name = "[NGUI] " + shader.name;
			mDynamicMat.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
		}
	}

    static UIRoot _root = null;

    void CalMaterialOffset()
    {
        if (panel.clipCount > 0)
        {
            Transform node = panel.cachedTransform;
            UIRoot root = NGUITools.FindInParents<UIRoot>(node);

            if (root != null)
            {
                _root = root;
                rootScale = (float)Screen.height / root.activeHeight;
            }
        }        
    }

    /// <summary>
    /// Rebuild the draw call's material.
    /// </summary>

    Material RebuildMaterial ()
	{
        // Destroy the old material
#if NGUI_BACKUP
        NGUITools.DestroyImmediate(mDynamicMat);
#else
        if (mDynamicMat != null)
        {
            mDynamicMat.Destroy();
            mDynamicMat = null;
        }
#endif

        // Create a new material
        CreateMaterial();
		mDynamicMat.renderQueue = mRenderQueue;

		// Update the renderer
		if (mRenderer != null)
		{
			mRenderer.sharedMaterials = new Material[] { mDynamicMat };
			mRenderer.sortingLayerName = mSortingLayerName;
			mRenderer.sortingOrder = mSortingOrder;            
		}
		return mDynamicMat;
	}

	/// <summary>
	/// Update the renderer's materials.
	/// </summary>

	void UpdateMaterials ()
	{
		if (panel == null) return;

		// If clipping should be used, we need to find a replacement shader
		if (mRebuildMat || mDynamicMat == null || mClipCount != panel.clipCount || mTextureClip != (panel.clipping == Clipping.TextureMask))
		{
			RebuildMaterial();            
			mRebuildMat = false;
		}
    }

	static ColorSpace mColorSpace = ColorSpace.Uninitialized;

	/// <summary>
	/// Set the draw call's geometry.
	/// </summary>

	public void UpdateGeometry (int widgetCount)
	{
		this.widgetCount = widgetCount;
		int vertexCount = verts.Count;

		// Safety check to ensure we get valid values
		if (vertexCount > 0 && (vertexCount == uvs.Count && vertexCount == cols.Count) && (vertexCount % 4) == 0)
		{
			if (mColorSpace == ColorSpace.Uninitialized)
				mColorSpace = QualitySettings.activeColorSpace;

			if (mColorSpace == ColorSpace.Linear)
			{
				for (int i = 0; i < vertexCount; ++i)
				{
					var c = cols[i];
					c.r = Mathf.GammaToLinearSpace(c.r);
					c.g = Mathf.GammaToLinearSpace(c.g);
					c.b = Mathf.GammaToLinearSpace(c.b);
					c.a = Mathf.GammaToLinearSpace(c.a);
					cols[i] = c;
				}
			}

			// Cache all components
			if (mFilter == null) mFilter = gameObject.GetComponent<MeshFilter>();
			if (mFilter == null) mFilter = gameObject.AddComponent<MeshFilter>();

			if (vertexCount < 65000)
			{
				// Populate the index buffer
				int indexCount = (vertexCount >> 1) * 3;
#if NGUI_BACKUP || UNITY_4_7
                bool setIndices = (mIndices == null || mIndices.Length != indexCount);                
#else
                bool setIndices = (mIndices == null || mIndices.Count != indexCount);
#endif

                // Create the mesh
                if (mMesh == null)
				{
					mMesh = new Mesh();
					mMesh.hideFlags = HideFlags.DontSave;
					mMesh.name = (mMaterial != null) ? "[NGUI] " + mMaterial.name : "[NGUI] Mesh";
					if (dx9BugWorkaround == 0) mMesh.MarkDynamic();
					setIndices = true;                    
                }
#if !UNITY_FLASH && NGUI_BACKUP
				// If the buffer length doesn't match, we need to trim all buffers
				bool trim = uvs.Count != vertexCount || cols.Count != vertexCount || uv2.Count != vertexCount || norms.Count != vertexCount || tans.Count != vertexCount;

				// Non-automatic render queues rely on Z position, so it's a good idea to trim everything
				if (!trim && panel != null && panel.renderQueue != UIPanel.RenderQueue.Automatic)
					trim = (mMesh == null || mMesh.vertexCount != verts.Count);

				// NOTE: Apparently there is a bug with Adreno devices:
				// http://www.tasharen.com/forum/index.php?topic=8415.0
#if !UNITY_ANDROID
				// If the number of vertices in the buffer is less than half of the full buffer, trim it
				if (!trim && (vertexCount << 1) < verts.Count) trim = true;
#endif
#endif
                mTriangles = (vertexCount >> 1);

				if (mMesh.vertexCount != vertexCount)
				{
					mMesh.Clear();
					setIndices = true;
				}
#if UNITY_4_7
				var hasUV2 = (uv2 != null && uv2.Count == vertexCount);
				var hasNormals = (norms != null && norms.Count == vertexCount);
				var hasTans = (tans != null && tans.Count == vertexCount);

				if (mTempVerts == null || mTempVerts.Length < vertexCount) mTempVerts = new Vector3[vertexCount];
				if (mTempUV0 == null || mTempUV0.Length < vertexCount) mTempUV0 = new Vector2[vertexCount];
				if (mTempCols == null || mTempCols.Length < vertexCount) mTempCols = new Color[vertexCount];

				if (hasUV2 && (mTempUV2 == null || mTempUV2.Length < vertexCount)) mTempUV2 = new Vector2[vertexCount];
				if (hasNormals && (mTempNormals == null || mTempNormals.Length < vertexCount)) mTempNormals = new Vector3[vertexCount];
				if (hasTans && (mTempTans == null || mTempTans.Length < vertexCount)) mTempTans = new Vector4[vertexCount];

				verts.CopyTo(mTempVerts);
				uvs.CopyTo(mTempUV0);
				cols.CopyTo(mTempCols);

				if (hasNormals) norms.CopyTo(mTempNormals);
				if (hasTans) tans.CopyTo(mTempTans);
				if (hasUV2) for (int i = 0, imax = verts.Count; i < imax; ++i) mTempUV2[i] = uv2[i];

				mMesh.vertices = mTempVerts;
				mMesh.uv = mTempUV0;
				mMesh.colors = mTempCols;
				mMesh.uv2 = hasUV2 ? mTempUV2 : null;
				mMesh.normals = hasNormals ? mTempNormals : null;
				mMesh.tangents = hasTans ? mTempTans : null;
#else
#if NGUI_BACKUP
                mMesh.SetVertices(verts);
				mMesh.SetUVs(0, uvs);
				mMesh.SetColors(cols);
#else
                mMesh.vertices = verts.ToArray2();
                mMesh.uv = uvs.ToArray2();
                mMesh.colors = cols.ToArray2();
#endif

#if UNITY_5_4 || UNITY_5_5_OR_NEWER
#if NGUI_BACKUP
                mMesh.SetUVs(1, (uv2.Count == vertexCount) ? uv2 : null);
                mMesh.SetNormals((norms.Count == vertexCount) ? norms : null);
                mMesh.SetTangents((tans.Count == vertexCount) ? tans : null);
#else
                if (uv2.Count == vertexCount) mMesh.uv2 = uv2.ToArray2();
                if (norms.Count == vertexCount) mMesh.normals = norms.ToArray2();
                if (tans.Count == vertexCount) mMesh.tangents = tans.ToArray2();
#endif
#else
                if (uv2.Count != vertexCount) uv2.Clear();
				if (norms.Count != vertexCount) norms.Clear();
				if (tans.Count != vertexCount) tans.Clear();

				mMesh.SetUVs(1, uv2);
				mMesh.SetNormals(norms);
				mMesh.SetTangents(tans);
#endif
#endif
                if (setIndices)
				{
#if NGUI_BACKUP || UNITY_4_7
                    mIndices = GenerateCachedIndexBuffer(vertexCount, indexCount);
                    mMesh.triangles = mIndices;
#else
                    mIndices = GenerateIndexBuffer(vertexCount);
                    mMesh.SetTriangles(mIndices, 0);
#endif
                }

#if NGUI_BACKUP
                if (trim || !alwaysOnScreen)                
#else
                if (!alwaysOnScreen)
#endif
                    mMesh.RecalculateBounds();

				mFilter.mesh = mMesh;
			}
			else
			{
				mTriangles = 0;
				if (mMesh != null) mMesh.Clear();
				Debug.LogError("Too many vertices on one panel: " + vertexCount);
			}

			if (mRenderer == null) mRenderer = gameObject.GetComponent<MeshRenderer>();

			if (mRenderer == null)
			{
				mRenderer = gameObject.AddComponent<MeshRenderer>();
                mRenderer.motionVectors = false;
#if UNITY_EDITOR
				mRenderer.enabled = isActive;
#endif
#if !UNITY_4_7
				if (mShadowMode == ShadowMode.None)
				{
					mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
					mRenderer.receiveShadows = false;
				}
				else if (mShadowMode == ShadowMode.Receive)
				{
					mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
					mRenderer.receiveShadows = true;
				}
				else
				{
					mRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
					mRenderer.receiveShadows = true;
				}
#endif
			}

			if (mIsNew)
			{
				mIsNew = false;
				if (onCreateDrawCall != null) onCreateDrawCall(this, mFilter, mRenderer);
			}

			UpdateMaterials();
		}
		else
		{
			if (mFilter.mesh != null) mFilter.mesh.Clear();
			Debug.LogError("UIWidgets must fill the buffer with 4 vertices per quad. Found " + vertexCount);
		}

		verts.Clear();
		uvs.Clear();
		uv2.Clear();
		cols.Clear();
		norms.Clear();
		tans.Clear();
	}

#if !NGUI_BACKUP
    void ClearMeshBuffers()
    {
        verts.Dispose();
        uvs.Dispose();
        uv2.Dispose();
        cols.Dispose();
        norms.Dispose();
        tans.Dispose();
    }
#endif

    const int maxIndexBufferCache = 10;

#if NGUI_BACKUP || UNITY_4_7
#if UNITY_FLASH
	List<int[]> mCache = new List<int[]>(maxIndexBufferCache);
#else
    static List<int[]> mCache = new List<int[]>(maxIndexBufferCache);
#endif

    /// <summary>
    /// Generates a new index buffer for the specified number of vertices (or reuses an existing one).
    /// </summary>    
    int[] GenerateCachedIndexBuffer (int vertexCount, int indexCount)
	{
		for (int i = 0, imax = mCache.Count; i < imax; ++i)
		{
			int[] ids = mCache[i];
			if (ids != null && ids.Length == indexCount)
				return ids;
		}

		int[] rv = new int[indexCount];
		int index = 0;

		for (int i = 0; i < vertexCount; i += 4)
		{
			rv[index++] = i + 2;
			rv[index++] = i + 1;
			rv[index++] = i;

			rv[index++] = i;
			rv[index++] = i + 3;
			rv[index++] = i + 2;
		}

		if (mCache.Count > maxIndexBufferCache) mCache.RemoveAt(0);
		mCache.Add(rv);
		return rv;
	}
#else
    List<int> GenerateIndexBuffer(int vertexCount)
    {
        int count = vertexCount * 6 / 4;

        if (mIndices.Count > count)
        {
            mIndices.RemoveRange(count, mIndices.Count - count);
        }
        else if (mIndices.Count < count)
        {
            int index = mIndices.Count * 4 / 6;

            for (int i = index; i < vertexCount; i += 4)
            {
                mIndices.Add(i + 2);
                mIndices.Add(i + 1);
                mIndices.Add(i);

                mIndices.Add(i);
                mIndices.Add(i + 3);
                mIndices.Add(i + 2);
            }
        }

        return mIndices;
    }
#endif

    /// <summary>
    /// This function is called when it's clear that the object will be rendered.
    /// We want to set the shader used by the material, creating a copy of the material in the process.
    /// We also want to update the material's properties before it's actually used.
    /// </summary>

    protected MaterialPropertyBlock mBlock;

    void OnWillRenderObject()
    {
        UpdateMaterials();

        if (mBlock != null) mRenderer.SetPropertyBlock(mBlock);
        if (onRender != null) onRender(mDynamicMat ?? mMaterial);
#if NGUI_BACKUP
        if (mDynamicMat == null || mClipCount == 0) return;
#else
        if (mDynamicMat == null || mClipCount == 0 || mDynamicMat.frameCount == Time.frameCount) return;
        mDynamicMat.frameCount = Time.frameCount;
#endif

        if (mTextureClip)
        {
            Vector4 cr = panel.drawCallClipRange;
            Vector2 soft = panel.clipSoftness;

            Vector2 sharpness = new Vector2(1000.0f, 1000.0f);
            if (soft.x > 0f) sharpness.x = cr.z / soft.x;
            if (soft.y > 0f) sharpness.y = cr.w / soft.y;

            mDynamicMat.SetVector(ClipRange[0], new Vector4(-cr.x / cr.z, -cr.y / cr.w, 1f / cr.z, 1f / cr.w));
            mDynamicMat.SetTexture("_ClipTex", clipTexture);
        }
        else if (!mLegacyShader)
        {
            UIPanel currentPanel = panel;

            for (int i = 0; currentPanel != null;)
            {
                if (currentPanel.hasClipping)
                {
                    float angle = 0f;
                    rootScale = (float)Screen.height / _root.activeHeight;
                    Vector4 cr = currentPanel.drawCallClipRange * rootScale;
                    Transform node = panel.cachedTransform;

                    // Clipping regions past the first one need additional math
                    if (currentPanel != panel)
                    {
                        Vector3 pos = currentPanel.cachedTransform.InverseTransformPoint(node.position);
                        cr.x -= pos.x * rootScale;
                        cr.y -= pos.y * rootScale;

                        Vector3 v0 = node.rotation.eulerAngles;
                        Vector3 v1 = currentPanel.cachedTransform.rotation.eulerAngles;
                        Vector3 diff = v1 - v0;

                        diff.x = NGUIMath.WrapAngle(diff.x);
                        diff.y = NGUIMath.WrapAngle(diff.y);
                        diff.z = NGUIMath.WrapAngle(diff.z);

                        if (Mathf.Abs(diff.x) > 0.001f || Mathf.Abs(diff.y) > 0.001f)
                            Debug.LogWarning("Panel can only be clipped properly if X and Y rotation is left at 0", panel);

                        angle = diff.z;
                        Vector2 offset = panel.clipOffset;

                        if (angle != 0)
                        {
                            Rotate(ref offset, -angle * Mathf.Deg2Rad);
                        }

                        cr.x -= offset.x * rootScale;
                        cr.y -= offset.y * rootScale;

                        // Pass the clipping parameters to the shader                    
                        SetClipping(i++, cr, currentPanel.clipSoftness, angle);
                    }
                    else
                    {
                        if (panel.cachedTransform.rotation == Quaternion.identity)
                        {
                            cr.x += node.position.x * (float)Screen.height / 2.0f;
                            cr.y += node.position.y * (float)Screen.height / 2.0f;

                            // Pass the clipping parameters to the shader                    
                            SetClipping(i++, cr, panel.clipSoftness, 0, 1);
                        }
                        else
                        {
                            Vector3 diff = panel.cachedTransform.eulerAngles;
                            diff.z = NGUIMath.WrapAngle(diff.z);
                            angle = diff.z;
                            angle *= Mathf.Deg2Rad;
                            Rotate(ref cr, angle);
                            cr.x += node.position.x * (float)Screen.height / 2.0f;
                            cr.y += node.position.y * (float)Screen.height / 2.0f;
                            angle = -angle;

                            // Pass the clipping parameters to the shader                    
                            SetClipping(i++, cr, panel.clipSoftness, Mathf.Sin(angle), Mathf.Cos(angle));
                        }
                    }
                }
                currentPanel = currentPanel.parentPanel;
            }
        }
        else // Legacy functionality
        {
            Vector2 soft = panel.clipSoftness;
            Vector4 cr = panel.drawCallClipRange;
            Vector2 v0 = new Vector2(-cr.x / cr.z, -cr.y / cr.w);
            Vector2 v1 = new Vector2(1f / cr.z, 1f / cr.w);

            Vector2 sharpness = new Vector2(1000.0f, 1000.0f);
            if (soft.x > 0f) sharpness.x = cr.z / soft.x;
            if (soft.y > 0f) sharpness.y = cr.w / soft.y;

            mDynamicMat.mainTextureOffset = v0;
            mDynamicMat.mainTextureScale = v1;
            mDynamicMat.SetVector("_ClipSharpness", sharpness);
        }
    }

    void Rotate(ref Vector2 offset, float angle)
    {
        float s = Mathf.Sin(angle);
        float c = Mathf.Cos(angle);
        float x = offset.x * c - offset.y * s;
        float y = offset.x * s + offset.y * c;
        offset.x = x;
        offset.y = y;        
    }

    void Rotate(ref Vector4 offset, float angle)
    {
        float s = Mathf.Sin(angle);
        float c = Mathf.Cos(angle);
        float x = offset.x * c - offset.y * s;
        float y = offset.x * s + offset.y * c;
        offset.x = x;
        offset.y = y;
    }

    void SetClipping(int index, Vector4 cr, Vector2 soft, float s, float c)
    {        
        Vector2 sharpness = new Vector2(1000.0f, 1000.0f);
        if (soft.x > 0f) sharpness.x = cr.z / soft.x;
        if (soft.y > 0f) sharpness.y = cr.w / soft.y;

        if (index < ClipRange.Length)
        {
            mDynamicMat.SetVector(ClipRange[index], new Vector4(cr.x, cr.y, 1f / cr.z, 1f / cr.w));
            mDynamicMat.SetVector(ClipArgs[index], new Vector4(sharpness.x, sharpness.y, s, c));
        }
    }

    static int[] ClipRange = null;
	static int[] ClipArgs = null;

	/// <summary>
	/// Set the shader clipping parameters.
	/// </summary>

	void SetClipping (int index, Vector4 cr, Vector2 soft, float angle)
	{
		angle *= -Mathf.Deg2Rad;

		Vector2 sharpness = new Vector2(1000.0f, 1000.0f);
		if (soft.x > 0f) sharpness.x = cr.z / soft.x;
		if (soft.y > 0f) sharpness.y = cr.w / soft.y;

		if (index < ClipRange.Length)
		{
			mDynamicMat.SetVector(ClipRange[index], new Vector4(-cr.x / cr.z, -cr.y / cr.w, 1f / cr.z, 1f / cr.w));
			mDynamicMat.SetVector(ClipArgs[index], new Vector4(sharpness.x, sharpness.y, Mathf.Sin(angle), Mathf.Cos(angle)));
		}
	}

    // Unity 5.4 bug work-around: http://www.tasharen.com/forum/index.php?topic=14839.0
    static int dx9BugWorkaround = -1;

	/// <summary>
	/// Cache the property IDs.
	/// </summary>

	void Awake ()
	{
        mTrans = transform;

        if (dx9BugWorkaround == -1)
		{
			var pf = Application.platform;
#if !UNITY_5_5_OR_NEWER
			dx9BugWorkaround = ((pf == RuntimePlatform.WindowsPlayer || pf == RuntimePlatform.XBOX360) &&
#else
			dx9BugWorkaround = ((pf == RuntimePlatform.WindowsPlayer) &&
#endif
				SystemInfo.graphicsShaderLevel < 40 && SystemInfo.graphicsDeviceVersion.Contains("Direct3D")) ? 1 : 0;
		}

		if (ClipRange == null)
		{
			ClipRange = new int[]
			{
				Shader.PropertyToID("_ClipRange0"),
				Shader.PropertyToID("_ClipRange1"),
				Shader.PropertyToID("_ClipRange2"),
				Shader.PropertyToID("_ClipRange4"),
			};
		}

		if (ClipArgs == null)
		{
			ClipArgs = new int[]
			{
				Shader.PropertyToID("_ClipArgs0"),
				Shader.PropertyToID("_ClipArgs1"),
				Shader.PropertyToID("_ClipArgs2"),
				Shader.PropertyToID("_ClipArgs3"),
			};
		}
	}

    /// <summary>
    /// The material should be rebuilt when the draw call is enabled.
    /// </summary>

    void OnEnable()
    {
#if NGUI_BACKUP
        mRebuildMat = true;
#else
        if (batch == BatchMode.Static || mDynamicMat == null)
        {
            mRebuildMat = true;            
        }
#endif
    }

	/// <summary>
	/// Clear all references.
	/// </summary>

	void OnDisable ()
	{
		depthStart = int.MaxValue;
		depthEnd = int.MinValue;
		panel = null;
		manager = null;
		mMaterial = null;
		mTexture = null;
		clipTexture = null;

		if (mRenderer != null)
			mRenderer.sharedMaterials = new Material[] {};
#if NGUI_BACKUP
        NGUITools.DestroyImmediate(mDynamicMat);
#else
        ClearMeshBuffers();

        if (mDynamicMat != null)
        {
            mDynamicMat.Destroy();
        }
#endif

        mDynamicMat = null;
	}

	/// <summary>
	/// Cleanup.
	/// </summary>

	void OnDestroy ()
	{
        NGUITools.DestroyImmediate(mMesh);
		mMesh = null;
	}

	/// <summary>
	/// Return an existing draw call.
	/// </summary>

	static public UIDrawCall Create (UIPanel panel, Material mat, Texture tex, Shader shader)
	{
#if UNITY_EDITOR
		string name = null;
		if (tex != null) name = tex.name;
		else if (shader != null) name = shader.name;
		else if (mat != null) name = mat.name;
		return Create(name, panel, mat, tex, shader);
#else
		return Create(null, panel, mat, tex, shader);
#endif
	}

#if !NGUI_BACKUP

    static public UIDrawCall Clone(UIDrawCall dc)
    {
        UIDrawCall newDC = Create(dc.panel, dc.baseMaterial, dc.mainTexture, dc.shader);
        newDC.mDynamicMat = dc.mDynamicMat;
        dc.mDynamicMat.AddRef();
        newDC.mRebuildMat = false;
        newDC.mClipCount = dc.panel.clipCount;
        newDC.rootScale = dc.rootScale;
        newDC.batch = BatchMode.Dynamic;
        return newDC;
    }

    public void UpdateRender()
    {
        if (batch == BatchMode.Dynamic && mDynamicMat != null)
        {
            mDynamicMat.renderQueue = mRenderQueue;            

            if (mRenderer != null)
            {
                mRenderer.sharedMaterials = new Material[] { mDynamicMat };
                mRenderer.sortingLayerName = mSortingLayerName;
                mRenderer.sortingOrder = mSortingOrder;
                if (mBlock != null) mRenderer.SetPropertyBlock(mBlock);                
            }
        }
    }
#endif

    /// <summary>
    /// Create a new draw call, reusing an old one if possible.
    /// </summary>

    static UIDrawCall Create (string name, UIPanel pan, Material mat, Texture tex, Shader shader)
	{
		UIDrawCall dc = Create(name);
		dc.gameObject.layer = pan.cachedGameObject.layer;
		dc.baseMaterial = mat;
		dc.mainTexture = tex;
		dc.shader = shader;
		dc.renderQueue = pan.startingRenderQueue;
		dc.sortingOrder = pan.sortingOrder;
		dc.manager = pan;
        dc.batch = BatchMode.Static;
		return dc;
	}

	/// <summary>
	/// Create a new draw call, reusing an old one if possible.
	/// </summary>

	static UIDrawCall Create (string name)
	{
#if SHOW_HIDDEN_OBJECTS && UNITY_EDITOR
		name = (name != null) ? "_UIDrawCall [" + name + "]" : "DrawCall";
#endif
		while (mInactiveList.size > 0)
		{
			UIDrawCall dc = mInactiveList.Pop();

			if (dc != null)
			{
				mActiveList.Add(dc);
				if (name != null) dc.name = name;
                NGUITools.SetActive(dc.gameObject, true);
                return dc;
			}
		}

#if UNITY_EDITOR
		// If we're in the editor, create the game object with hide flags set right away
		GameObject go = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags(name,
#if SHOW_HIDDEN_OBJECTS
		HideFlags.DontSave | HideFlags.NotEditable, typeof(UIDrawCall));
#else
		HideFlags.HideAndDontSave, typeof(UIDrawCall));
#endif
		UIDrawCall newDC = go.GetComponent<UIDrawCall>();
#else
		GameObject go = new GameObject(name);
		DontDestroyOnLoad(go);
		UIDrawCall newDC = go.AddComponent<UIDrawCall>();
#endif
		// Create the draw call
		mActiveList.Add(newDC);
		return newDC;
	}

    /// <summary>
    /// Clear all draw calls.
    /// </summary>

    static public void ClearAll ()
	{
		bool playing = Application.isPlaying;

		for (int i = mActiveList.size; i > 0; )
		{
			UIDrawCall dc = mActiveList[--i];

			if (dc)
			{
#if SHOW_HIDDEN_OBJECTS && UNITY_EDITOR
				if (UnityEditor.Selection.activeGameObject == dc.gameObject)
					UnityEditor.Selection.activeGameObject = null;
#endif
				if (playing) NGUITools.SetActive(dc.gameObject, false);
				else NGUITools.DestroyImmediate(dc.gameObject);
			}
		}
		mActiveList.Clear();
	}

	/// <summary>
	/// Immediately destroy all draw calls.
	/// </summary>

	static public void ReleaseAll ()
	{
		ClearAll();
		ReleaseInactive();
	}

	/// <summary>
	/// Immediately destroy all inactive draw calls (draw calls that have been recycled and are waiting to be re-used).
	/// </summary>

	static public void ReleaseInactive()
	{
		for (int i = mInactiveList.size; i > 0; )
		{
			UIDrawCall dc = mInactiveList[--i];

			if (dc)
			{
#if SHOW_HIDDEN_OBJECTS && UNITY_EDITOR
				if (UnityEditor.Selection.activeGameObject == dc.gameObject)
					UnityEditor.Selection.activeGameObject = null;
#endif
				NGUITools.DestroyImmediate(dc.gameObject);
			}
		}
		mInactiveList.Clear();
	}

	/// <summary>
	/// Count all draw calls managed by the specified panel.
	/// </summary>

	static public int Count (UIPanel panel)
	{
		int count = 0;
		for (int i = 0; i < mActiveList.size; ++i)
			if (mActiveList[i].manager == panel) ++count;
		return count;
	}

	/// <summary>
	/// Destroy the specified draw call.
	/// </summary>

	static public void Destroy (UIDrawCall dc)
	{
		if (dc)
		{
			if (dc.onCreateDrawCall != null)
			{
                dc.transform.parent = null;
				NGUITools.Destroy(dc.gameObject);
				return;
			}

			dc.onRender = null;

			if (Application.isPlaying)
			{
				if (mActiveList.Remove(dc))
				{
					NGUITools.SetActive(dc.gameObject, false);
#if !NGUI_BACKUP
                    if (dc.mRenderer) dc.mRenderer.enabled = true;
#endif
                    mInactiveList.Add(dc);
					dc.mIsNew = true;
				}
			}
			else
			{
				mActiveList.Remove(dc);
#if SHOW_HIDDEN_OBJECTS && UNITY_EDITOR
				if (UnityEditor.Selection.activeGameObject == dc.gameObject)
					UnityEditor.Selection.activeGameObject = null;
#endif
				NGUITools.DestroyImmediate(dc.gameObject);
			}
		}
	}

#if !UNITY_4_7 && !UNITY_5_0 && !UNITY_5_1 && !UNITY_5_2 && !UNITY_5_3
	/// <summary>
	/// Move all draw calls to the specified scene.
	/// http://www.tasharen.com/forum/index.php?topic=13965.0
	/// </summary>

	static public void MoveToScene (UnityEngine.SceneManagement.Scene scene)
	{
		foreach (var dc in activeList) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dc.gameObject, scene);
		foreach (var dc in inactiveList) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dc.gameObject, scene);
	}
#endif
}
