using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ProductionWorkshopLinkRenderer
{
    private GameObject root;
    private Material material;

    public void Show(
        BuildableObject building,
        IProductionWorkshopRuntime workshops)
    {
        Clear();
        if (building == null || workshops == null)
        {
            return;
        }

        IReadOnlyList<ProductionSupportLinkSnapshot> links;
        if (building.BuildingData.GetProductionWorkstationAbility() != null)
        {
            links = workshops.GetLinks(building);
        }
        else if (workshops.TryGetLinkForSupport(
                     building,
                     out ProductionSupportLinkSnapshot supportLink))
        {
            links = new[] { supportLink };
        }
        else
        {
            return;
        }

        if (links.Count == 0)
        {
            return;
        }

        root = new GameObject("ProductionWorkshopConnections");
        for (int index = 0; index < links.Count; index++)
        {
            ProductionSupportLinkSnapshot link = links[index];
            if (link?.Workstation == null || link.Support == null)
            {
                continue;
            }

            GameObject lineObject = new GameObject($"Connection_{index}");
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.07f;
            line.endWidth = 0.07f;
            line.numCapVertices = 3;
            line.startColor = new Color(0.96f, 0.72f, 0.22f, 0.9f);
            line.endColor = new Color(0.4f, 0.85f, 0.95f, 0.9f);
            line.sortingOrder = 60;
            line.sharedMaterial = GetMaterial();
            Vector3 start = link.Workstation.transform.position;
            Vector3 end = link.Support.transform.position;
            start.z = -0.5f;
            end.z = -0.5f;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    public void Clear()
    {
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
            root = null;
        }
    }

    private Material GetMaterial()
    {
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(
            "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        shader ??= Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        material = new Material(shader)
        {
            name = "ProductionWorkshopConnectionMaterial"
        };
        return material;
    }
}
