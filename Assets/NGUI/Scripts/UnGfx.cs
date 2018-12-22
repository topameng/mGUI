/*
作者: 蒙占志 日期: 2012-1-13
作用: 和unity相关的一些Gfx函数
*/
#define __NGUI__

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
//using System.Net.NetworkInformation;
using LuaInterface;

public class UnFrustum
{
    public UnFrustum(float fLeft, float fRight, float fTop, float fBottom, float fNear, float fFar, bool bOrtho)
    {
        left = fLeft;
        right = fRight;
        top = fTop;
        bottom = fBottom;
        near = fNear;
        far = fFar;
        ortho = bOrtho;
    }

    public float left;
    public float right;
    public float top;
    public float bottom;
    public float near;
    public float far;
    public bool ortho = false;
};


public static class UnGfx
{
    public const float UN_PI = 3.1415927410125732f;
    public const float HALF_PI = 1.5707963705062866f;
    public const float TWO_PI = 6.2831854820251464f;

    public static Vector3 hidePos = new Vector3(65536, 65536, 65536);

    public static RaycastHit[] rayHits = new RaycastHit[10];

    public static Collider[] colliders = new Collider[128];

    public static string Space = "\u3000";

    //替代避免每次内存分配
    public readonly static string dataPath = Application.dataPath;
    public readonly static string persistentDataPath = Application.persistentDataPath;
    public readonly static string temporaryCachePath = Application.temporaryCachePath;
    public readonly static string streamingAssetsPath = Application.streamingAssetsPath;
    public readonly static string unityVersion = Application.unityVersion;

    static UnGfx()
    {
        dataPath = Application.dataPath.Replace('\\', '/');
        persistentDataPath = Application.persistentDataPath.Replace('\\', '/');
        temporaryCachePath = Application.temporaryCachePath.Replace('\\', '/');
        streamingAssetsPath = Application.streamingAssetsPath.Replace('\\', '/');        
    }

    public static string loadedLevelName
    {
        get
        {
#if UNITY_5_3_OR_NEWER            
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.name;
#else
            return Application.loadedLevelName;
#endif
        }
    }

    public static int loadedLevel
    {
        get
        {
#if UNITY_5_3_OR_NEWER
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.buildIndex;
#else
            return Application.loadedLevel;
#endif
        }
    }

    public static UnFrustum GetCameraFrustum(Camera camera)
    {
        float aspet = (float)Screen.width / Screen.height;
        float fovRad = UN_PI / 180 * camera.fieldOfView;
        float height = Mathf.Tan(fovRad / 2);
        float width = height * aspet;
        bool ortho = camera.orthographic;

        return new UnFrustum(-width, width, height, -height, camera.nearClipPlane, camera.farClipPlane, ortho);
    }

    public static void MouseToRay(float fX, float fY, Camera camera, out Vector3 kOrigin, out Vector3 kDirection)
    {
        float fUnitizedX = (fX / Screen.width) * 2.0f - 1.0f;
        float fUnitizedY = ((Screen.height - fY) / Screen.height) * 2.0f - 1.0f;

        UnFrustum frustum = GetCameraFrustum(camera);

        fUnitizedX *= frustum.right;
        fUnitizedY *= frustum.top;

        Matrix4x4 kRotation = camera.transform.localToWorldMatrix;

        Vector3 kLook, kLookUp, kLookRight;
        kLook = kRotation.GetColumn(0);
        kLookUp = kRotation.GetColumn(1);
        kLookRight = kRotation.GetColumn(2);

        if (frustum.ortho)
        {
            kOrigin = camera.transform.position + kLookRight * fUnitizedX + kLookUp * fUnitizedY;
            kDirection = kLook;
        }
        else
        {
            kOrigin = camera.transform.position;
            kDirection = kLook + kLookUp * fUnitizedY + kLookRight * fUnitizedX;
            kDirection.Normalize();
        }
    }


    public static Vector3 TranslateOnPlane(Vector3 start, Vector3 normal, Vector3 origin, Vector3 dir)
    {
        float distance = Vector3.Dot(start - origin, normal);
        float compDirN = Vector3.Dot(dir, normal);
        return origin + dir * (distance / compDirN);
    }

    public static void Attach(this Transform parent, Transform child)
    {
        Attach(parent, child, true);
    }

    /*挂接dummy点在指定节点*/
    public static void Attach(Transform parent, Transform child, bool keepLocal)
    {
        if (parent == null)
        {
            return;
        }

        if (keepLocal)
        {
            Vector3 pos = child.localPosition;
            Quaternion rot = child.localRotation;
            Vector3 scale = child.localScale;
            child.parent = parent;
            child.localPosition = pos;
            child.localRotation = rot;
            child.localScale = scale;
        }
        else
        {
            child.parent = parent;
        }
    }
    
    //gc alloc 比 path 方式多, 但用法方便
    public static Transform FindNode(this Transform node, string name)
    {
        if (node.name == name)
        {
            return node;
        }
        
        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);
            t = FindNode(t, name);

            if (t != null)
            {
                return t;
            }
        }
        
        return null;
    }

    public static Transform FindNode(this GameObject go, string name)
    {
        Transform node = go.transform;
        return FindNode(node, name);
    }

    public static T GetComponentInChildren<T>(this GameObject go, string name) where T : Component
    {
        Transform node = go.transform;
        node = FindNode(node, name);

        if (node == null)
        {
            return null;
        }

        GameObject obj = node.gameObject;
        return obj.GetComponent<T>();
    }


    public static Transform FindNodeNoCase(this Transform node, string name)
    {
        if (string.Compare(node.name, name, true) == 0)
        {
            return node;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);
            t = FindNode(t, name);

            if (t != null)
            {
                return t;
            }
        }

        return null;
    }

    public static void FindNodeList(Transform node, string name, List<Transform> list)
    {
        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);

            if (t.name == name)
            {
                list.Add(t);
            }

            FindNodeList(t, name, list);
        }        
    }


    public static void TraversalNode(Transform node, Action<Transform> call)
    {
        call(node);

        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);
            TraversalNode(t, call);
        } 
    }

    public static void SetLayerRecursively(Transform node, int layer)
    {
        node.gameObject.layer = layer;        

        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);
            SetLayerRecursively(t, layer);
        } 
    }

#if UNITY_5
    public static void SetShadowRecursively(Transform node, UnityEngine.Rendering.ShadowCastingMode cast, bool recieve)
    {
        Renderer r = node.GetComponent<Renderer>();

        if (r)
        {
            r.shadowCastingMode = cast;
            r.receiveShadows = recieve;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);
            SetShadowRecursively(t, cast, recieve);
        }
    }
#else
    public static void SetShadowRecursively(Transform node, bool cast, bool recieve)
    {
        Renderer r = node.GetComponent<Renderer>();

        if (r)
        {            
            r.castShadows = cast;
            r.receiveShadows = recieve;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            Transform t = node.GetChild(i);
            SetShadowRecursively(t, cast, recieve);
        } 
    }
#endif

    public static void ReplaceNode(Transform node, Transform attach)
    {
        Vector3 pos = node.localPosition;
        Quaternion rot = node.localRotation;
        Vector3 scale = node.localScale;
        attach.parent = node.parent;
        attach.name = node.name;
        attach.localPosition = pos;
        attach.localRotation = rot;
        attach.localScale = scale;

        GameObject.DestroyObject(node.gameObject);
    }

    public static void Detach(Transform node)
    {
        node.parent = null;
    }

    public static UnityEngine.Object Load(string path)
    {
        return Load(path, typeof(UnityEngine.Object));
    }

    public static UnityEngine.Object Load(string path, Type type)
    {
        UnityEngine.Object obj = Resources.Load(path, type);

        if (!obj)
        {
            Debugger.LogError(string.Format("Resource load fail, {0} does not exists!!!", path));
            return null;
        }

        return obj;
    }

    public static T Load<T>(string path) where T : UnityEngine.Object
    {
        return Load(path, typeof(T)) as T;
    }

    public static GameObject CreateGameObject(string path)
    {
        UnityEngine.Object prefab = Load(path);
        GameObject obj = GameObject.Instantiate(prefab) as GameObject;
        return obj;
    }

    public static void SetObjectCull(GameObject obj, bool hide)
    {        
        Renderer[] renders = obj.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renders.Length; i++)
        {
            renders[i].enabled = !hide;
        }
    }

    public static void FastHide(Transform node)
    {
        node.position = hidePos;
    }

    public static void QuaternionNormalize(ref Quaternion q)
    {
        float invMag = 1.0f / Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        q.x *= invMag;
        q.y *= invMag;
        q.z *= invMag;
        q.w *= invMag;
    }

    public static Matrix4x4 Convert(Quaternion q)
    {
        Matrix4x4 mat = new Matrix4x4();
        Convert(q, ref mat);
        return mat;
    }

    public static void Convert(Quaternion q, ref Matrix4x4 mat)
    {
        QuaternionNormalize(ref q);
        mat.SetTRS(Vector3.zero, q, Vector3.one);
    }

    //可能这个转换有问题
    public static void Convert(ref Matrix4x4 mat, ref Quaternion q)
    {
        q.w = Mathf.Sqrt(Mathf.Max(0, 1 + mat.m00 + mat.m11 + mat.m22)) / 2f;
        q.x = Mathf.Sqrt(Mathf.Max(0, 1 + mat.m00 - mat.m11 - mat.m22)) / 2f;
        q.y = Mathf.Sqrt(Mathf.Max(0, 1 - mat.m00 + mat.m11 - mat.m22)) / 2f;
        q.z = Mathf.Sqrt(Mathf.Max(0, 1 - mat.m00 - mat.m11 + mat.m22)) / 2f;

        q.x *= Mathf.Sign(q.x * (mat.m21 - mat.m12));
        q.y *= Mathf.Sign(q.y * (mat.m02 - mat.m20));
        q.z *= Mathf.Sign(q.z * (mat.m10 - mat.m01));
        
        QuaternionNormalize(ref q);
    }

    static int[] _next = new int[3] { 1, 2, 0 };

    static void MatrixToQuaternion(ref Matrix4x4 kRot, ref Quaternion q)
    {
        float trace = kRot.m00 + kRot.m11 + kRot.m22;        

        if (trace > 0.0f)
        {            
            float s = Mathf.Sqrt(trace + 1.0f);  // 2w
            q.w = 0.5f * s;
            s = 0.5f / s;  // 1/(4w)
            q.x = (kRot.m21 - kRot.m12) * s;
            q.y = (kRot.m02 - kRot.m20) * s;
            q.z = (kRot.m10 - kRot.m01) * s;
            QuaternionNormalize(ref q);
        }
        else
        {                        
            int i = 0;

            if (kRot.m11 > kRot.m00)
            {
                i = 1;
            }

            if (kRot.m22 > kRot[i, i])
            {
                i = 2;
            }

            int j = _next[i];
            int k = _next[j];

            float s = Mathf.Sqrt(kRot[i, i] - kRot[j, j] - kRot[k, k] + 1.0f);
            float[] quat = new float[3] {0, 0, 0};            

            quat[i] = 0.5f * s;
            s = 0.5f / s;
            q.w = (kRot[k, j] - kRot[j, k]) * s;
            quat[j] = (kRot[j, i] + kRot[i, j]) * s;
            quat[k] = (kRot[k, i] + kRot[i, k]) * s;
            q.Set(quat[0], quat[1], quat[2], q.w);
            QuaternionNormalize(ref q);
        }
    }

    public static Quaternion ToQuaternion(this Matrix4x4 mat)
    {
        Quaternion quat = Quaternion.identity;
        MatrixToQuaternion(ref mat, ref quat);        
        return quat;
    }

    public static Vector3 GetAxisX(this Quaternion q)
    {
        float tx = q.x + q.x;
        float ty = q.y + q.y;
        float tz = q.z + q.z;

        float x = 1.0f - (q.y * ty + q.z * tz);
        float y = q.y * tx + q.w * tz;
        float z = q.z * tx - q.w * ty;

        return new Vector3(x, y, z);
    }

    public static Vector3 GetAxisY(this Quaternion q)
    {
        float tx = q.x + q.x;
        float ty = q.y + q.y;
        float tz = q.z + q.z;

        float x = q.y * tx - q.w * tz;
        float y = 1.0f - (q.x * tx + q.z * tz);
        float z = q.z * ty + q.w * tx;

        return new Vector3(x, y, z);
    }

    public static Vector3 GetAxisZ(this Quaternion q)
    {
        float tx = q.x + q.x;
        float ty = q.y + q.y;

        float x = q.z * tx + q.w * ty;
        float y = q.z * ty - q.w * tx;
        float z = 1.0f - (q.x * tx + q.y * ty);

        return new Vector3(x, y, z);
    }

    public static Vector3 InvScale(this Vector3 v)
    {
        return new Vector3(1.0f / v.x, 1.0f / v.y, 1.0f / v.z);
    }

    public static void Convert(Bounds bound, ref BoxCollider bc)
    {
        bc.center = bound.center;
        bc.size = bound.size;
        bc.size = bound.size;
    }

    public static void SetIgnoreCollisionLayer(int layer)
    {
        for (int j = 0; j < 32; j++)
        {
            int mask = 1 << j;
            int bit = mask & layer;

            if (bit != 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    Physics.IgnoreLayerCollision(j, i, true);
                }
            }
        }
    }

    public static T GetSafeComponent<T>(this GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();

        if (!component)
        {
            component = obj.AddComponent<T>();
        }

        return component;
    }

    public static Component GetSafeComponent(this GameObject go, Type t)
    {
        Component component = go.GetComponent(t);

        if (!component)
        {
            component = go.AddComponent(t);
        }

        return component;
    }

    public static T GetSafeComponent<T>(this Transform node) where T : Component
    {        
        T component = node.GetComponent<T>();

        if (!component)
        {
            component = node.gameObject.AddComponent<T>();
        }

        return component;
    }

    public static Component GetSafeComponent(this Transform node, Type t)
    {        
        Component component = node.GetComponent(t);

        if (component != null)
        {            
            component = node.gameObject.AddComponent(t);
        }

        return component;
    }

    public static T GetFirstComponentUpward<T>(this GameObject go) where T : Component
    {
        if (go != null)
        {
            Component[] array = go.GetComponents<Component>();

            for (int i = 0; i < array.Length; i++)
            {
                T t = array[i] as T;

                if (t != null)
                {
                    return t;
                }
            }

            Transform parent = go.transform.parent;

            if (parent != null && parent.gameObject != null)
            {
                return GetFirstComponentUpward<T>(parent.gameObject);
            }
        }

        return default(T);
    }

    public static void AddCollider(GameObject obj)
    {
        MeshFilter[] filters = obj.GetComponentsInChildren<MeshFilter>() as MeshFilter[];

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            Mesh mesh = filter.mesh;

            MeshCollider mc = UnGfx.GetSafeComponent<MeshCollider>(filter.gameObject);
            mc.sharedMesh = mesh;
        }
    }

    public static Material GetMainMaterial(GameObject obj)
    {
        Renderer r = obj.GetComponent<Renderer>();

        if (r != null)
        {
            return r.material;
        }

        return null;
    }

    public static void CopyLocalPRS(this Transform newTran, Transform oldTran)
    {
        newTran.localPosition = oldTran.localPosition;
        newTran.localRotation = oldTran.localRotation;
        newTran.localScale = oldTran.localScale;        
    }

    public static float GetSkinMeshHeight(GameObject obj)
    {
        SkinnedMeshRenderer smr = obj.GetComponentInChildren<SkinnedMeshRenderer>();
        return smr == null ? 0 : smr.bounds.size.y;
    }

    /*public static long ProfileMemoryBegin()
    {
        //long memory = GC.GetTotalMemory(false);
        //float mb = (float)memory / (1024f * 1024f);
        //Debugger.Log("Total allocated memory : {0} MBytes", mb);

        return GC.GetTotalMemory(false);
    }

    public static void ProfileMemoryEnd(string name, long memory)
    {
        GC.Collect(0);
        memory -= GC.GetTotalMemory(false);
        float mb = memory / (1024f * 1024f);
        Debugger.Log("{0} use memory: {1} Mbytes", name, mb);
    }*/

    //模拟ngui panel渐隐消失
    /*public static void SetMaterialClip(Material mat, Vector4 v4)
    {
        float w = Screen.width / 2;
        float h = Screen.height / 2;
        v4.x /= w;
        v4.y /= h;
        v4.z /= 2 * w;
        v4.w /= 2 * h;
        mat.SetVector("_Clip", v4);
    }*/

    //修改Render Queue排序值
    public static void SetRenderQueue(GameObject obj, string matName, int z)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i].material.shader.name == matName)
            {
                rends[i].material.renderQueue = z;
            }
        }
    } 

    public static string GetTransformPath(Transform trans)
    {        
        StringBuilder sb = new StringBuilder();
        sb.Append(trans.name);
        Transform node = trans.parent;

        while (node != null)
        {            
            sb.Insert(0, '/');
            sb.Insert(0, node.name);
            node = node.parent;
        }
       
        return sb.ToString();
    }

    public static void Identity(Transform trans)
    {
        trans.localScale = Vector3.one;
        trans.localPosition = Vector3.zero;
        trans.localRotation = Quaternion.identity;
    }

    public static string LoadTextAsset(string path)
    {
        string str = null;
        TextAsset text = UnGfx.Load(path) as TextAsset;

        if (text != null)
        {
            str = text.ToString();                                    
            Resources.UnloadAsset(text);            
        }

        return str;
    }

    //U5.x 多了很多新格式，但常用的还是这些
    static int GetBitsPerPixel(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Alpha8: 
                return 8;
            case TextureFormat.ARGB4444:
                return 16;
            case TextureFormat.RGBA4444:
                return 16;
            case TextureFormat.RGB24:
                return 24;
            case TextureFormat.RGBA32:
                return 32;
            case TextureFormat.ARGB32:
                return 32;
            case TextureFormat.BGRA32:
                return 32;
            case TextureFormat.RGB565:
                return 16;
            case TextureFormat.DXT1:
                return 4;
            case TextureFormat.DXT5:
                return 8;
            case TextureFormat.PVRTC_RGB2:
                return 2;
            case TextureFormat.PVRTC_RGBA2:
                return 2;
            case TextureFormat.PVRTC_RGB4:
                return 4;
            case TextureFormat.PVRTC_RGBA4:
                return 4;
            case TextureFormat.ETC_RGB4:
                return 4;
            case TextureFormat.ATC_RGB4:
                return 4;
            case TextureFormat.ATC_RGBA8:
                return 8;
            case TextureFormat.ETC2_RGB:
                return 4;
            case TextureFormat.ETC2_RGBA8:
                return 8;
            case TextureFormat.ETC2_RGBA1:
                return 5;
            default:            
                return 0; 
        }        
    }

    public static int GetTextureSizeBytes(Texture tex)
    {
        int width = tex.width;
        int height = tex.height;

        if (tex is Texture2D)
        {
            Texture2D tTex2D = tex as Texture2D;
            int bitsPerPixel = GetBitsPerPixel(tTex2D.format);
            int mipMapCount = tTex2D.mipmapCount;
            int mipLevel = 1;
            int size = 0;

            while (mipLevel <= mipMapCount)
            {
                size += width * height * bitsPerPixel / 8;
                width = width / 2;
                height = height / 2;
                mipLevel++;
            }

            return size;
        }

        if (tex is Cubemap)
        {
            Cubemap tCubemap = tex as Cubemap;
            int bitsPerPixel = GetBitsPerPixel(tCubemap.format);
            return width * height * 6 * bitsPerPixel / 8;
        }

        return 0;
    }

    public static TextureFormat GetTextureFormat(Texture tex)
    {
        if (tex is Texture2D)
        {
            Texture2D tTex2D = (Texture2D)tex;
            return tTex2D.format;
        }

        if (tex is Cubemap)
        {
            Cubemap tCubemap = tex as Cubemap;
            return tCubemap.format;            
        }

        return 0;
    }

    /// <summary>
    /// 设置屏幕分辨率
    /// </summary>    
    public static void SetResolution(int height, bool flag = true)
    {
        float w = Screen.width;
        float h = Screen.height;
        w = w * height / h;
        int width = (int)w;
        width &= ~1;
        Screen.SetResolution(width, height, flag);        
    }

    public static GameObject GetObjectsInScene(string name)
    {
#if UNITY_5_3_OR_NEWER
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        GameObject[] allObjs = scene.GetRootGameObjects();

        for (int i = 0; i < allObjs.Length; i++)
        {
            GameObject go = allObjs[i];

            if (go.name == name)
            {
                return go;
            }
        }
#else
        GameObject[] allObjs = (GameObject[])Resources.FindObjectsOfTypeAll(typeof(GameObject));

        for (int i = 0; i < allObjs.Length; i++)
        {
            GameObject go = allObjs[i];

            if (go.transform.parent != null)
            {
                continue;
            }

            if (go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave)
            {
                continue;
            }

            if (go.name == name)
            {
                return go;
            }
        }
#endif

        return null;
    }

    //可以找到 disable 的节点
    public static GameObject FindGameObject(string path)
    {
        string[] strs = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        int count = strs.Length;
        
        if (count <= 0) 
        {
            return null;
        }
        
        GameObject go = GameObject.Find(strs[0]);

        if (go == null)
        {
            go = GetObjectsInScene(strs[0]);

            if (go == null)
            {
                return null;
            }
        }

        Transform node = go.transform;

        for (int i = 1; i < strs.Length; i++)
        {
            node = node.Find(strs[i]);

            if (node == null)
            {
                return null;
            }
        }

        return node.gameObject;
    }

    //苹果不让用了
    //public static string GetMacAddress()
    //{
    //    NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

    //    for (int i = 0; i < nics.Length; i++)
    //    {
    //        PhysicalAddress address = nics[i].GetPhysicalAddress();

    //        if (address.ToString() != "")
    //        {
    //            return address.ToString();
    //        }
    //    }

    //    return "";
    //}

    public static void Collect()
    {
        Resources.UnloadUnusedAssets();
        System.GC.Collect(0);
    }

    public static string CurrentJoystickName()
    {
        string[] joysticks = Input.GetJoystickNames();

        if (joysticks != null && joysticks.Length > 0)
        {
            if (joysticks.Length > 1)
            {
                Debugger.LogWarning("More then 1 gamepad connected, getting name of first.");
            }

            return joysticks[0];
        }
        
        return null;
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        angle = angle % 360;

        if ((angle >= -360f) && (angle <= 360f))
        {
            if (angle < -360f)
            {
                angle += 360f;
            }
            if (angle > 360f)
            {
                angle -= 360f;
            }
        }

        return Mathf.Clamp(angle, min, max);
    }

    /// <summary>
    /// 规范角度为-180-180. 欧拉角范围
    /// </summary>        
    public static float ClampEulerAngle(float angle)
    {
        if (angle > 180)
        {
            angle -= 360;
        }
        else if (angle < -180)
        {
            angle += 360;
        }

        return angle;
    }

    /// <summary>
    /// 针对两个点做lerp插值，t范围0-1，进行 sin 曲线化
    /// </summary>
    /// <param name="start">插值起始点</param>
    /// <param name="end">插值结束点</param>
    /// <param name="t">[0,1]</param>        
    public static Vector3 Sinerp(Vector3 start, Vector3 end, float t)
    {
        t = Mathf.Sin(t * Mathf.PI * 0.5f);
        return new Vector3(Mathf.Lerp(start.x, end.x, t), Mathf.Lerp(start.y, end.y, t), Mathf.Lerp(start.z, end.z, t));
    }

    /// <summary>
    /// 在一个锥形范围内随机产生一个方向矢量
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="ConeAngle"></param>
    /// <returns></returns>
    public static Vector3 RandomVectorInsideCone(Vector3 dir, float angle)
    {        
        float a = UnityEngine.Random.Range(0.0f, Mathf.PI * 2f);
        float b = UnityEngine.Random.Range(0.0f, angle * Mathf.Deg2Rad); // should be in range [0,180]

        float tmp = Mathf.Sin(b);
        Vector3 vec = new Vector3(Mathf.Sin(a) * tmp, Mathf.Cos(a) * tmp, Mathf.Cos(b));
        
        return Quaternion.LookRotation(dir) * vec;
    }

    /// <summary>
    /// 返回两条之间最近的点
    /// </summary>
    public static Vector3 ClosePointOfTwoLines(Vector3 start1, Vector3 end1, Vector3 start2, Vector3 end2)
    {
        Vector3 u = end1 - start1;
        Vector3 v = end2 - start2;
        Vector3 w = start1 - start2;

        float a = Vector3.Dot(u, u);            // always >= 0
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);            // always >= 0
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);
        float det = a * c - b * b;              // always >= 0
        float sc;

        
        if (det < Mathf.Epsilon)              //平行线
        {
            sc = 0.0f;
        }
        else
        {
            sc = (b * e - c * d) / det;
        }
        
        if (sc < 0)
        {
            return start1;
        }

        if (sc * sc > u.sqrMagnitude)
        {
            return end1;
        }

        return start1 + u * sc;        
    }

    public static bool Approximately(Quaternion a, Quaternion b)
    {
        return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y) && Mathf.Approximately(a.z, b.z) && Mathf.Approximately(a.w, b.w);
    }

    public static int CompareRayCastHit(RaycastHit x, RaycastHit y)
    {
        return x.distance.CompareTo(y.distance);
    }

	public static Vector3 Str2Vector3(string str)
	{
        float x, y, z;
		string[] ss = str.Split(new char[]{','}, System.StringSplitOptions.RemoveEmptyEntries);

        if (ss.Length < 3)
        {
            return Vector3.zero;
        }        

        if (float.TryParse(ss[0], out x) && float.TryParse(ss[1], out y) && float.TryParse(ss[2], out z))
        {
            return new Vector3(x, y, z);
        }

		return Vector3.zero;
	}
		
	public static bool CalculateGravityAngle(float s, float h, float sp, float g, out float angle)
    {
		//                           +   为高射
		angle = Mathf.Atan ((sp * sp - Mathf.Sqrt (Mathf.Pow (sp, 4) - g * (g * s * s + 2 * h * sp * sp))) / (g * s));
		angle *= Mathf.Rad2Deg;
        
		if (float.IsNaN (angle)) 
        {
			Debugger.LogError ("Speed is not enough, can't hit the target（YI）！！！");
			return false;
		}

		return true;
	}    

#if  __NGUI__
    public static void SetUICameraFlags(CameraClearFlags flag)
    {
        if (UICamera.list.size > 0)
        {
            Camera cam = UICamera.list[0].cachedCamera;
            cam.clearFlags = flag;
        }
    }
#endif

    /// <summary>
    /// 将Unix时间戳转换为DateTime类型时间
    /// </summary>
    /// <param name="d">double 型数字</param>
    /// <returns>DateTime</returns>
    public static System.DateTime ConvertIntDateTime(double d)
    {
        DateTime date = new DateTime(1970, 1, 1).AddSeconds(d);
        return date;
    }

    public static long GenerateTimeStamp(System.DateTime dt)
    {
        System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
        long timeStamp = (long)(dt - startTime).TotalSeconds; // 相差秒数
        return timeStamp;
    }

    public static int NextPowerOfTwo(int v)
    {
        v -= 1;
        v |= v >> 16;
        v |= v >> 8;
        v |= v >> 4;
        v |= v >> 2;
        v |= v >> 1;
        return v + 1;
    }
}

public class CompareRaycastHit : IComparer<RaycastHit>
{
    public int Compare(RaycastHit x, RaycastHit y)
    {
        return x.distance.CompareTo(y.distance);
    }
}
