using System.Diagnostics.CodeAnalysis;
using NHSRemont.Environment.Fractures;
using NHSRemont.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace NHSRemont.Tools
{
    [RequireComponent(typeof(BoxCollider))]
    public class ConvexWallWithWindows : MonoBehaviour
    {
        const string generatedParentName = "(generated)";
    
        [SerializeField] private int numWindows = 1;
        [SerializeField] private Vector2 windowSize = new Vector2(1f, 1.5f);
        [Tooltip("How high above the ground are the windows?")]
        [SerializeField] private float windowHeight = 1f;
        [Tooltip("How far are the windows shifted to the side?")]
        [SerializeField] private float windowOffset = 0f;
        [SerializeField] private Material outsideMaterial, insideMaterial;

        [ContextMenu("Generate")]
        [SuppressMessage("ReSharper", "LocalVariableHidesMember")]
        public void GenerateWallWithWindows()
        {
            RemoveGeneratedGameobjects();
            AutoDetectMaterials();

            //window dimensions relative to transform scaling
            float windowHeight = this.windowHeight / transform.localScale.y;
            float windowOffset = this.windowOffset / transform.localScale.x;
            Vector2 windowSize = new Vector2(this.windowSize.x / transform.localScale.x,
                this.windowSize.y / transform.localScale.y);
            
            Transform generatedParent = new GameObject(generatedParentName).transform;
            generatedParent.SetParent(transform, false);

            BoxCollider wallCollider = gameObject.GetComponent<BoxCollider>();
            Fracturable fracture = gameObject.GetComponent<Fracturable>();
            bool useSubmeshes = fracture == null; //fractures do not support multiple submeshes

            Vector3 wallSize = wallCollider.size;
            Vector3 wallCentre = wallCollider.center;
            
            //create bottom cube
            if (windowHeight > 0)
            {
                Vector3 bottomCubeSize = new Vector3(wallSize.x, windowHeight, wallSize.z);
                Vector3 bottomCubeCentre = new Vector3(wallCentre.x,
                    wallCentre.y + (windowHeight / 2f) - wallSize.y / 2f, wallCentre.z);
                Transform bottomCube = CreateCube(generatedParent, bottomCubeSize, Vector2.zero, useSubmeshes);
                bottomCube.localPosition = bottomCubeCentre;
                bottomCube.name = "Bottom Cube";
                if (fracture)
                    Fracturable.CopyToSimilarObject(fracture, bottomCube.gameObject,
                        VectorUtils.Divide(bottomCubeSize, wallSize), false);
            }

            //create middle cubes
            //numWindows * windowSize.x + (numWindows+1) * middleCubeWidth = wallSize.x
            //therefore (by algebra):
            float middleCubeWidth = (wallSize.x - numWindows * windowSize.x) / (numWindows + 1);
            Vector3 middleCubeSize = new Vector3(middleCubeWidth, windowSize.y, wallSize.z);
            for (int i = 0; i < numWindows+1; i++)
            {
                float offsetFromLeft = i*(middleCubeSize.x + windowSize.x); //offset of the left side of this window from the left side of the wall
                Vector3 thisCubeSize = middleCubeSize;
                if (i == 0)
                {
                    thisCubeSize += Vector3.right*windowOffset;
                }
                else if (i == numWindows)
                {
                    thisCubeSize -= Vector3.right * windowOffset;
                    offsetFromLeft += windowOffset;
                }
                else
                {
                    offsetFromLeft += windowOffset;
                }
                Vector3 middleCubeCorner = new Vector3(offsetFromLeft, windowHeight, 0f);
                Vector3 middleCubeCentre = wallCentre + middleCubeCorner + (thisCubeSize/2f) - wallSize/2f;
                Transform middleCube = CreateCube(generatedParent, thisCubeSize, middleCubeCorner, useSubmeshes);
                middleCube.localPosition = middleCubeCentre;
                middleCube.name = "Middle Cube (" + i + ")";
                if (fracture)
                {
                    Fracturable.CopyToSimilarObject(fracture, middleCube.gameObject,
                        VectorUtils.Divide(thisCubeSize, wallSize), false);
                }
            }

            //create top cube
            float topCubeBottomHeight = windowHeight + windowSize.y; //the local y position at which the bottom of the top cube starts
            float topCubeSizeY = wallSize.y - topCubeBottomHeight;
            Vector3 topCubeSize = new Vector3(wallSize.x, topCubeSizeY, wallSize.z);
            Vector3 topCubeCentre = new Vector3(wallCentre.x, wallCentre.y + topCubeBottomHeight + (topCubeSizeY / 2f) - wallSize.y/2f, wallCentre.z);
            Transform topCube = CreateCube(generatedParent, topCubeSize, new Vector2(0f, topCubeBottomHeight), useSubmeshes);
            topCube.localPosition = topCubeCentre;
            topCube.name = "Top Cube";
            if(fracture)
                Fracturable.CopyToSimilarObject(fracture, topCube.gameObject,
                    VectorUtils.Divide(topCubeSize, wallSize), false);

            //disable own components
            var rend = GetComponent<Renderer>();
            if (rend) rend.enabled = false;
            GetComponent<BoxCollider>().enabled = false;
            if (fracture && !fracture.independent)
                fracture.enabled = false;
        }

        [ContextMenu("Remove Generated")]
        public void RemoveGeneratedGameobjects()
        {
            Transform generatedParent = transform.Find(generatedParentName);
            if(generatedParent == null) return;
            DestroyImmediate(generatedParent.gameObject);
            
            var rend = GetComponent<Renderer>();
            if (rend) rend.enabled = true;
            GetComponent<BoxCollider>().enabled = true;
            var frac = GetComponent<Fracturable>();
            if (frac) frac.enabled = true;
        }

        private void AutoDetectMaterials()
        {
            if (outsideMaterial == null)
            {
                outsideMaterial = GetComponent<Renderer>()?.sharedMaterial;
            }

            if (insideMaterial == null)
            {
                Renderer rend = GetComponent<Renderer>();
                if (!rend)
                {
                    insideMaterial = outsideMaterial;
                }
                else
                {
                    var mats = rend.sharedMaterials;
                    insideMaterial = mats.Length > 1 ? mats[1] : mats[0];
                }
            }
        }

        private Transform CreateCube(Transform parent, Vector3 size, Vector2 uvOffset, bool useSubmeshes)
        {
            GameObject cube = new GameObject("Cube");
            cube.transform.SetParent(parent, false);

            Mesh mesh = CreateCubeMesh(size, uvOffset, useSubmeshes);
            cube.AddComponent<GeneratedMeshPersistence>().SetMesh(mesh);
            MeshRenderer rend = cube.AddComponent<MeshRenderer>();
            if (useSubmeshes)
                rend.sharedMaterials = new[] {insideMaterial, outsideMaterial};
            else
                rend.sharedMaterial = outsideMaterial;
            BoxCollider boxCollider = cube.AddComponent<BoxCollider>();
            boxCollider.center = Vector3.zero;
            boxCollider.size = size;

            return cube.transform;
        }

        private Mesh CreateCubeMesh(Vector3 size, Vector2 uvOffset, bool useSubmeshes)
        {
            Vector3[] corners = new Vector3[]
            {
                new Vector3(-size.x, -size.y, -size.z)/2f, //bottom left back
                new Vector3(-size.x, -size.y, size.z)/2f, //bottom left front
                new Vector3(size.x, -size.y, -size.z)/2f, //bottom right back
                new Vector3(size.x, -size.y, size.z)/2f, //bottom right front
                new Vector3(-size.x, size.y, -size.z)/2f, //top left back
                new Vector3(-size.x, size.y, size.z)/2f, //top left front
                new Vector3(size.x, size.y, -size.z)/2f, //top right back
                new Vector3(size.x, size.y, size.z)/2f, //top right front
            };

            Vector3[] vertices = new Vector3[]
            {
                corners[0], corners[1], corners[2], corners[3], //bottom verts
                corners[4], corners[5], corners[6], corners[7], //top verts
                corners[0], corners[1], corners[4], corners[5], //left verts
                corners[2], corners[3], corners[6], corners[7], //right verts
                corners[0], corners[2], corners[4], corners[6], //back verts
                corners[1], corners[3], corners[5], corners[7], //front verts
            };

            Vector2 uv00 = new Vector2(0, 0);
            Vector2 uv01 = new Vector2(0, size.y);
            Vector2 uv10x = new Vector2(size.x, 0);
            Vector2 uv11x = new Vector2(size.x, size.y);
            Vector2 uv10z = new Vector2(size.z, 0);
            Vector2 uv11z = new Vector2(size.z, size.y);

            Vector2 offsetXY = uvOffset;
            Vector2 offsetZY = new Vector2(0, uvOffset.y);

            Vector2[] uvs = new Vector2[]
            {
                uv00 + offsetZY, uv01 + offsetZY, uv10z + offsetZY, uv11z + offsetZY, //bottom
                uv00 + offsetZY, uv01 + offsetZY, uv10z + offsetZY, uv11z + offsetZY, //top
                uv00 + offsetZY, uv10z + offsetZY, uv01 + offsetZY, uv11z + offsetZY, //left
                uv10z + offsetZY, uv00 + offsetZY, uv11z + offsetZY, uv01 + offsetZY, //right
                uv10x + offsetXY, uv00 + offsetXY, uv11x + offsetXY, uv01 + offsetXY, //back
                uv00 + offsetXY, uv10x + offsetXY, uv01 + offsetXY, uv11x + offsetXY //front
            };
            
            Vector3[] normals = new Vector3[]
            {
                Vector3.down, Vector3.down, Vector3.down, Vector3.down,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };

            int[] triangles = new int[]
            {
                0,3,1, 0,2,3, //bottom face
                4,5,7, 4,7,6, //top face
                8,9,11, 8,11,10, //left face
                12,15,13, 12,14,15, //right face
                16,19,17, 16,18,19, //back face
                20,21,23, 20,23,22 //front face
            };

            Mesh mesh = new Mesh
            {
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles,
            };
            if (useSubmeshes)
            {
                mesh.SetSubMeshes(new[]
                {
                    new SubMeshDescriptor(0 * 3, 8 * 3),
                    new SubMeshDescriptor(8 * 3, 4 * 3)
                });
            }

            mesh.Optimize();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}