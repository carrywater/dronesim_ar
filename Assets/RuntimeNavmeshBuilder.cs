using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Meta.XR.MRUtilityKit;

using System;
public class RuntimeNavmeshBuilder : MonoBehaviour
{
     private NavMeshSurface navmeshSurface;
    void Start()
    {   
        navmeshSurface = GetComponent<NavMeshSurface>();
        MRUK.Instance.RegisterSceneLoadedCallback(BuildNavMesh);
        navmeshSurface.BuildNavMesh();
    }

public void BuildNavMesh()
{
    StartCoroutine(BuildNavMeshRoutine());
}
public IEnumerator BuildNavMeshRoutine()
{
    yield return new WaitForEndOfFrame();
    navmeshSurface.BuildNavMesh();
}




}
