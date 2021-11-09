using System.Collections.Generic;
using NHSRemont.Utility;
using UnityEngine;

namespace NHSRemont.Environment
{
    /// <summary>
    /// Stores trees within a small area, which can be drawn efficiently using drawTrees()
    /// </summary>
    public class TreeChunk
    {
        private readonly Mesh treeMesh;
        private readonly Material[] treeMaterials;
        public readonly Mesh billboardMesh;
        public readonly Material billboardMaterial;
        
        private readonly QuadTree<TreeInstance> trees; //stores tree indices in a way that's easy to locate
        public Matrix4x4[] matrices { get; private set; }

        public bool drawAsMesh = false; //should the mesh be drawn?
        public bool drawAsBillboard = true; //should the billboard be drawn?

        public TreeChunk(Rect bounds, Mesh treeMesh, Material[] treeMaterials, Mesh billboardMesh, Material billboardMaterial)
        {
            trees = new QuadTree<TreeInstance>(bounds);
            this.treeMesh = treeMesh;
            this.treeMaterials = treeMaterials;
            this.billboardMesh = billboardMesh;
            this.billboardMaterial = billboardMaterial;
        }

        /// <summary>
        /// Adds a tree to this chunk, without updating the matrix array.
        /// </summary>
        /// <param name="tree">Instance of the tree to add</param>
        public void AddTree(TreeInstance tree)
        {
            trees.Add(tree, FlattenVectorToXZ(tree.position));
        }

        /// <summary>
        /// Removes a tree at the specified position, without updating the matrix array.
        /// </summary>
        /// <param name="pos">Normalised terrain XZ space position of the tree to remove</param>
        /// <returns>The removed tree</returns>
        public Maybe<TreeInstance> RemoveTree(Vector2 pos)
        {
            return trees.RemoveAtPosition(pos);
        }

        /// <summary>
        /// Gets the tree at a specified position
        /// </summary>
        /// <param name="pos">Normalised terrain XZ space position of the tree to remove</param>
        public Maybe<TreeInstance> GetTree(Vector2 pos)
        {
            return trees.GetAtPosition(pos);
        }

        /// <summary>
        /// Gets all the instances (and local x-z plane positions) of trees within the given bounding box
        /// </summary>
        /// <param name="bounds">Bounding box in normalised terrain XZ space</param>
        public List<(TreeInstance element, Vector2 position)> GetTreesWithinBounds(Rect bounds)
        {
            return trees.GetElementsWithinBounds(bounds);
        }

        public List<TreeInstance> GetAllTrees()
        {
            return trees.GetAllElements();
        }

        /// <summary>
        /// Updates the matrix array used to render trees so that it reflects the trees in this chunk.
        /// </summary>
        /// <param name="terrainPosition">The world position of this terrain</param>
        /// <param name="terrainSize">The size of this terrain</param>
        public void UpdateMatrixArray(Vector3 terrainPosition, Vector3 terrainSize)
        {
            var treesList = trees.GetAllElements();
            int amt = treesList.Count;
            matrices = new Matrix4x4[amt];
            for (int i = 0; i < amt; i++)
            {
                TreeInstance tree = treesList[i];
                float rotation = tree.rotation * Mathf.Rad2Deg;
                matrices[i] = Matrix4x4.TRS(
                    terrainPosition + ScaleVector3(tree.position, terrainSize), 
                    Quaternion.Euler(0f, rotation, 0f),
                    new Vector3(tree.widthScale, tree.heightScale, tree.widthScale)
                    );
            }
        }

        /// <summary>
        /// Draw the trees in this chunk for one frame
        /// </summary>
        public void DrawTrees()
        {
            if(matrices.Length == 0) return;
            
            if (drawAsMesh)
            {
                for (int i = 0; i < treeMesh.subMeshCount; i++)
                {
                    Graphics.DrawMeshInstanced(treeMesh, i, treeMaterials[i], matrices);
                }
            }
            if (drawAsBillboard)
            {
                Graphics.DrawMeshInstanced(billboardMesh, 0, billboardMaterial, matrices);
            }
        }

        /// <summary>
        /// How many matrices (one per tree as of last matrix array update) in this group?
        /// </summary>
        public int MatricesCount()
        {
            return matrices.Length;
        }

        /// <summary>
        /// Returns a vector (a.x*b.x, a.y*b.y, a.z*b.z)
        /// </summary>
        private Vector3 ScaleVector3(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        /// <summary>
        /// Returns a 2D vector representing the input vector's x and z positions.
        /// </summary>
        private Vector2 FlattenVectorToXZ(Vector3 vector3)
        {
            return new Vector2(vector3.x, vector3.z);
        }
    }
}