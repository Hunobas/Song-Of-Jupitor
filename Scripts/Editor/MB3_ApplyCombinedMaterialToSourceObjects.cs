namespace DigitalOpus.MB.Custom
{
	public class MB3_ApplyCombinedMaterialToSourceObjects
	{
        // ...
        
		public override void OnInspectorGUI()
		{
			//...
			
			if (GUILayout.Button("Apply Combined Material To Source Objects (No Mesh Combine)"))
			{
				for (int tIdx = 0; tIdx < targets.Length; tIdx++)
				{
					ApplyCombinedMaterialToSourceObjects((MB3_MeshBakerGrouper)targets[tIdx]);
				}
			}
		}
		
        private static void ApplyCombinedMaterialToSourceObjects(MB3_MeshBakerGrouper grouper)
        {
            MB3_TextureBaker tb = grouper.GetComponent<MB3_TextureBaker>();
            if (tb == null || tb.textureBakeResults == null)
            {
                Debug.LogError("TextureBaker or textureBakeResults is null");
                return;
            }

            MB2_TextureBakeResults tbr = tb.textureBakeResults;
            Material combinedMat = tbr.resultMaterials[0].combinedMaterial;
            
            List<GameObject> objsToCombine = tb.GetObjectsToCombine();
            int processedCount = 0;
            int skippedCount = 0;
            
            string meshOutputFolder = "Assets/GeneratedMeshes_AtlasUV";
            if (!AssetDatabase.IsValidFolder(meshOutputFolder))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedMeshes_AtlasUV");
            }
            
            foreach (GameObject go in objsToCombine)
            {
                if (go == null) continue;
                
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                MeshFilter mf = go.GetComponent<MeshFilter>();
                
                if (mr == null || mf == null || mf.sharedMesh == null) continue;
                
                Mesh originalMesh = mf.sharedMesh;
                Material[] originalMaterials = mr.sharedMaterials;
                
                // 서브메쉬 개수와 Material 개수 확인
                int subMeshCount = originalMesh.subMeshCount;
                
                if (subMeshCount != originalMaterials.Length)
                {
                    Debug.LogWarning($"[{go.name}] SubMesh count ({subMeshCount}) != Material count ({originalMaterials.Length}). Skipping.");
                    skippedCount++;
                    continue;
                }
                
                // 각 서브메쉬별로 UV Rect 수집
                Rect[] uvRects = new Rect[subMeshCount];
                bool allRectsFound = true;
                
                for (int i = 0; i < subMeshCount; i++)
                {
                    Material mat = originalMaterials[i];
                    Rect rect = GetUVRectForMaterial(tbr, mat);
                    
                    if (rect == Rect.zero)
                    {
                        if (mat == combinedMat)
                        {
                            rect = new Rect(0, 0, 1, 1);
                            Debug.Log($"[{go.name}] Material {i} is already CombinedMat, keeping UV as-is");
                        }
                        else
                        {
                            Debug.LogWarning($"[{go.name}] Could not find UV rect for material '{mat?.name}' at index {i}");
                            allRectsFound = false;
                            break;
                        }
                    }
                    
                    uvRects[i] = rect;
                }
                
                if (!allRectsFound)
                {
                    skippedCount++;
                    continue;
                }
                
                // Prefab 호환성을 위해 Mesh를 에셋으로 저장한다..
                Mesh newMesh = CreateRemappedMeshMultiSubmesh(originalMesh, uvRects);
                newMesh.name = originalMesh.name + "_AtlasUV";
                
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshOutputFolder}/{newMesh.name}.asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                
                mf.sharedMesh = newMesh;
                
                Material[] newMaterials = new Material[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                {
                    newMaterials[i] = combinedMat;
                }
                mr.sharedMaterials = newMaterials;
                
                EditorUtility.SetDirty(go);
                processedCount++;
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"Applied combined material to {processedCount} objects. Skipped {skippedCount} objects.");
        }

        private static Rect GetUVRectForMaterial(MB2_TextureBakeResults tbr, Material mat)
        {
            if (mat == null) return Rect.zero;
            
            for (int i = 0; i < tbr.materialsAndUVRects.Length; i++)
            {
                if (tbr.materialsAndUVRects[i].material == mat)
                {
                    return tbr.materialsAndUVRects[i].atlasRect;
                }
            }
            return Rect.zero;
        }

        private static Mesh CreateRemappedMeshMultiSubmesh(Mesh original, Rect[] atlasRects)
        {
            Mesh newMesh = Instantiate(original);
            
            Vector2[] uvs = newMesh.uv;
            
            // 각 서브메쉬별로 UV 재매핑
            for (int subMeshIdx = 0; subMeshIdx < original.subMeshCount; subMeshIdx++)
            {
                Rect atlasRect = atlasRects[subMeshIdx];
                
                // 이 서브메쉬가 사용하는 삼각형의 버텍스 인덱스들 가져오기
                int[] triangles = original.GetTriangles(subMeshIdx);
                
                // 해당 버텍스들의 UV만 변환 (HashSet으로 중복 방지)
                HashSet<int> processedVertices = new HashSet<int>();
                
                foreach (int vertIdx in triangles)
                {
                    if (processedVertices.Contains(vertIdx)) continue;
                    processedVertices.Add(vertIdx);
                    
                    // UV를 Atlas 좌표로 변환
                    uvs[vertIdx].x = atlasRect.x + uvs[vertIdx].x * atlasRect.width;
                    uvs[vertIdx].y = atlasRect.y + uvs[vertIdx].y * atlasRect.height;
                }
            }
            
            newMesh.uv = uvs;
            
            return newMesh;
        }
	}
}