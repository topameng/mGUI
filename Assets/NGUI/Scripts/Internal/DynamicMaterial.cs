using UnityEngine;
using System.Collections;

public class DynamicMaterial 
{
    public Material mat = null;
    public int frameCount = -1;    
    int count = 0;

    public int renderQueue
    {
        get
        {
            return mat.renderQueue;
        }

        set
        {
            mat.renderQueue = value;
        }
    }

    public string name
    {
        get
        {
            return mat.name;
        }

        set
        {
            mat.name = value;
        }
    }

    public Shader shader
    {
        get
        {
            return mat.shader;
        }

        set
        {
            mat.shader = value;
        }
    }

    public HideFlags hideFlags
    {
        get
        {
            return mat.hideFlags;
        }

        set
        {
            mat.hideFlags = value;
        }
    }

    public Vector2 mainTextureOffset
    {
        get
        {
            return mat.mainTextureOffset;
        }
        set
        {
            mat.mainTextureOffset = value;
        }
    }

    public Vector2 mainTextureScale
    {
        get
        {
            return mat.mainTextureScale;
        }

        set
        {
            mat.mainTextureScale = value;
        }
    }

    public DynamicMaterial(Material mat)
    {
        this.mat = new Material(mat);        
        count = 1;
    }

    public DynamicMaterial(Shader shader)        
    {
        this.mat = new Material(shader);
        count = 1;
    }

    public void Destroy()
    {
        if (--count <= 0 && mat != null)
        {            
            if (Application.isEditor)
            {
                UnityEngine.Object.DestroyImmediate(mat);
            }
            else
            {
                UnityEngine.Object.Destroy(mat);
            }
            
            mat = null;            
        }
    }

    public void AddRef()
    {
        ++count;
    }

    public void CopyPropertiesFromMaterial(Material m)
    {
        mat.CopyPropertiesFromMaterial(m);
    }

    public void EnableKeyword(string keyword)
    {
        mat.EnableKeyword(keyword);
    }

    public void SetVector(int nameID, Vector4 vector)
    {
        mat.SetVector(nameID, vector);
    }

    public void SetVector(string propertyName, Vector4 vector)
    {
        mat.SetVector(propertyName, vector);
    }

    public void SetTexture(int nameID, Texture texture)
    {
        mat.SetTexture(nameID, texture);
    }

    public void SetTexture(string propertyName, Texture texture)
    {
        mat.SetTexture(propertyName, texture);
    }

    public static implicit operator Material(DynamicMaterial self)
    {
        return self.mat;
    }
}
