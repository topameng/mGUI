//----------------------------------------------
//            NGUI: Next-Gen UI kit
// Copyright © 2011-2016 Tasharen Entertainment
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generated geometry class. All widgets have one.
/// This class separates the geometry creation into several steps, making it possible to perform
/// actions selectively depending on what has changed. For example, the widget doesn't need to be
/// rebuilt unless something actually changes, so its geometry can be cached. Likewise, the widget's
/// transformed coordinates only change if the widget's transform moves relative to the panel,
/// so that can be cached as well. In the end, using this class means using more memory, but at
/// the same time it allows for significant performance gains, especially when using widgets that
/// spit out a lot of vertices, such as UILabels.
/// </summary>

public class UIGeometry
{
    /// <summary>
    /// Widget's vertices (before they get transformed).
    /// </summary>

    public List<Vector3> verts
    {
        get
        {
            return _verts;
        }

        set
        {
            _verts = value;
        }
    }

    public List<Vector3> _verts = new List<Vector3>();

	/// <summary>
	/// Widget's texture coordinates for the geometry's vertices.
	/// </summary>

	public List<Vector2> uvs = new List<Vector2>();

	/// <summary>
	/// Array of colors for the geometry's vertices.
	/// </summary>

	public List<Color> cols = new List<Color>();

	/// <summary>
	/// Custom delegate called after WriteToBuffers finishes filling in the geometry.
	/// Use it to apply any and all modifications to vertices that you need.
	/// </summary>

	public OnCustomWrite onCustomWrite;
#if NGUI_BACKUP
    public delegate void OnCustomWrite (List<Vector3> v, List<Vector2> u, List<Color> c, List<Vector3> n, List<Vector4> t, List<Vector4> u2);
#else
    public delegate void OnCustomWrite(FreeList<Vector3> v, FreeList<Vector2> u, FreeList<Color> c, FreeList<Vector3> n, FreeList<Vector4> t, FreeList<Vector2> u2);
#endif

    // Relative-to-panel vertices, normal, and tangent
    List<Vector3> mRtpVerts = new List<Vector3>();
	Vector3 mRtpNormal;
	Vector4 mRtpTan;

	/// <summary>
	/// Whether the geometry contains usable vertices.
	/// </summary>

	public bool hasVertices { get { return (_verts.Count > 0); } }

	/// <summary>
	/// Whether the geometry has usable transformed vertex data.
	/// </summary>

	public bool hasTransformed { get { return (mRtpVerts != null) && (mRtpVerts.Count > 0) && (mRtpVerts.Count == _verts.Count); } }

	/// <summary>
	/// Step 1: Prepare to fill the buffers -- make them clean and valid.
	/// </summary>

	public void Clear ()
	{
        _verts.Clear();
		uvs.Clear();
		cols.Clear();
		mRtpVerts.Clear();
	}

#if !NGUI_BACKUP
    public void Destroy()
    {
        //verts.Dispose();
        //uvs.Dispose();
        //cols.Dispose();
        //mRtpVerts.Dispose();
    }
#endif

    /// <summary>
    /// Step 2: Transform the vertices by the provided matrix.
    /// </summary>

    public void ApplyTransform(ref Matrix4x4 mat/*widgetToPanel*/, bool generateNormals = true)
    {
        if (_verts.Count > 0)
        {
            mRtpVerts.Clear();
            for (int i = 0, imax = verts.Count; i < imax; ++i) mRtpVerts.Add(mat.MultiplyPoint3x4(verts[i]));

            // Calculate the widget's normal and tangent
            if (generateNormals)
            {
                mRtpNormal = mat.MultiplyVector(Vector3.back).normalized;
                Vector3 tangent = mat.MultiplyVector(Vector3.right).normalized;
                mRtpTan = new Vector4(tangent.x, tangent.y, tangent.z, -1f);
            }
        }
        else mRtpVerts.Clear();
    }

#if !NGUI_BACKUP
    public void SetSingle(bool flag)
    {
        //if (flag)
        //{
        //    mRtpVerts = verts;
        //}
        //else if (mRtpVerts == verts)
        //{
        //    mRtpVerts = new List<Vector3>();
        //}
    }

    public void UpdateRtpVert(bool flag)
    {
        if (flag && verts.Count > 0)
        {
            mRtpVerts.Clear();
            for (int i = 0, imax = verts.Count; i < imax; ++i) mRtpVerts.Add(verts[i]);
        }
    }

    public void UpdateTransform(ref Matrix4x4 mat/*widgetToPanel*/, bool generateNormals = true)
    {
        if (generateNormals)
        {
            mRtpNormal = mat.MultiplyVector(Vector3.back).normalized;
            Vector3 tangent = mat.MultiplyVector(Vector3.right).normalized;
            mRtpTan = new Vector4(tangent.x, tangent.y, tangent.z, -1f);
        }
    }
#endif

    /// <summary>
    /// Step 3: Fill the specified buffer using the transformed values.
    /// </summary>
#if NGUI_BACKUP
	public void WriteToBuffers (List<Vector3> v, List<Vector2> u, List<Color> c, List<Vector3> n, List<Vector4> t, List<Vector4> u2)
#else
    public void WriteToBuffers(FreeList<Vector3> v, FreeList<Vector2> u, FreeList<Color> c, FreeList<Vector3> n, FreeList<Vector4> t, FreeList<Vector2> u2)
#endif
    {
		if (mRtpVerts != null && mRtpVerts.Count > 0)
		{
			if (n == null)
			{
#if NGUI_BACKUP
                for (int i = 0, imax = mRtpVerts.Count; i < imax; ++i)
				{
					v.Add(mRtpVerts[i]);
					u.Add(uvs[i]);
					c.Add(cols[i]);
				}
#else
                v.AddRange(mRtpVerts);
                u.AddRange(uvs);
                c.AddRange(cols);
#endif
            }
			else
			{
#if NGUI_BACKUP
                for (int i = 0, imax = mRtpVerts.Count; i < imax; ++i)
				{
					v.Add(mRtpVerts[i]);
					u.Add(uvs[i]);
					c.Add(cols[i]);
					n.Add(mRtpNormal);
					t.Add(mRtpTan);
				}
#else
                v.AddRange(mRtpVerts);
                u.AddRange(uvs);
                c.AddRange(cols);

                for (int i = 0, imax = mRtpVerts.Count; i < imax; ++i)
                {
                    n.Add(mRtpNormal);
                    t.Add(mRtpTan);
                }
#endif
            }

            if (u2 != null)
			{
#if NGUI_BACKUP
                Vector4 uv2 = Vector4.zero;
#else
                Vector2 uv2 = Vector2.zero;
#endif

                for (int i = 0, imax = verts.Count; i < imax; ++i)
				{
					uv2.x = verts[i].x;
					uv2.y = verts[i].y;
					u2.Add(uv2);
				}
			}

			if (onCustomWrite != null) onCustomWrite(v, u, c, n, t, u2);
		}
	}
}
