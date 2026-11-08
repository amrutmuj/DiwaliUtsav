using System.Collections.Generic;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>Small procedural mesh helpers for the cracker models.</summary>
    public static partial class DiwaliSceneBuilder
    {
        /// <summary>Surface of revolution from a (radius, height) profile. V runs along the profile.</summary>
        static Mesh Lathe(Vector2[] profile, int segments = 40, bool planarUv = false)
        {
            int rows = profile.Length;
            int columns = segments + 1;
            var vertices = new Vector3[rows * columns];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();

            float total = 0;
            var travel = new float[rows];
            for (int i = 1; i < rows; i++)
            {
                total += Vector2.Distance(profile[i], profile[i - 1]);
                travel[i] = total;
            }

            float widest = 0;
            foreach (var point in profile) widest = Mathf.Max(widest, point.x);

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float angle = column * Mathf.PI * 2 / segments;
                    int index = row * columns + column;
                    float x = Mathf.Cos(angle) * profile[row].x;
                    float z = Mathf.Sin(angle) * profile[row].x;
                    vertices[index] = new Vector3(x, profile[row].y, z);
                    uv[index] = planarUv
                        ? new Vector2(x / widest * 0.5f + 0.5f, z / widest * 0.5f + 0.5f)
                        : new Vector2(column / (float)segments, total > 0 ? travel[row] / total : 0);
                }
            }

            for (int row = 0; row < rows - 1; row++)
            {
                for (int column = 0; column < segments; column++)
                {
                    int a = row * columns + column;
                    int b = a + columns;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }

            var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        /// <summary>A profile for a sphere of the given radius, centred on height.</summary>
        static Vector2[] SphereProfile(float radius, float height, int steps = 18)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float angle = -Mathf.PI / 2 + Mathf.PI * i / steps;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, height + Mathf.Sin(angle) * radius);
            }
            points[0].x = 0;
            points[steps].x = 0;
            return points;
        }

        /// <summary>Profile for a ring: a small circle swept around the axis.</summary>
        static Vector2[] RingProfile(float ringRadius, float tubeRadius, float height, int steps = 12)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.PI * 2 * i / steps;
                points[i] = new Vector2(ringRadius + Mathf.Cos(angle) * tubeRadius, height + Mathf.Sin(angle) * tubeRadius);
            }
            return points;
        }

        /// <summary>A printed carton: the upper three quarters of the texture wrap the sides, the rest is the lid.</summary>
        static Mesh Carton(float width, float height, float depth)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float x = width / 2, z = depth / 2;

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvMin, Vector2 uvMax)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                uv.Add(new Vector2(uvMin.x, uvMin.y)); uv.Add(new Vector2(uvMax.x, uvMin.y));
                uv.Add(new Vector2(uvMax.x, uvMax.y)); uv.Add(new Vector2(uvMin.x, uvMax.y));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }

            var sideMin = new Vector2(0, 0.25f);
            var sideMax = Vector2.one;
            Face(new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, height, -z), new Vector3(-x, height, -z), sideMin, sideMax);
            Face(new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(x, height, z), new Vector3(x, height, -z), sideMin, sideMax);
            Face(new Vector3(x, 0, z), new Vector3(-x, 0, z), new Vector3(-x, height, z), new Vector3(x, height, z), sideMin, sideMax);
            Face(new Vector3(-x, 0, z), new Vector3(-x, 0, -z), new Vector3(-x, height, -z), new Vector3(-x, height, z), sideMin, sideMax);
            Face(new Vector3(-x, height, -z), new Vector3(x, height, -z), new Vector3(x, height, z), new Vector3(-x, height, z), Vector2.zero, new Vector2(1, 0.25f));

            var mesh = new Mesh { vertices = vertices.ToArray(), uv = uv.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        /// <summary>The unit box a flame is ray marched through: x and z from -0.5 to 0.5, y from 0 to 1.</summary>
        static Mesh FlameBox()
        {
            if (flameBox) return flameBox;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 1, -.5f), new Vector3(-.5f, 1, -.5f),
                new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 1, .5f), new Vector3(-.5f, 1, .5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 1, 6, 5, 1, 2, 6, 5, 7, 4, 5, 6, 7,
                4, 3, 0, 4, 7, 3, 3, 6, 2, 3, 7, 6, 4, 1, 5, 4, 0, 1,
            };
            mesh.RecalculateBounds();
            flameBox = SaveMesh(mesh);
            return flameBox;
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position = default)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
