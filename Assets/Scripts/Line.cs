using UnityEngine;

public class Line {
    public GameObject obj;
    private LineRenderer lineRenderer;
    public int id;
    public Color color;
    public float width;

    private Vector3[] originalPoints = new Vector3[] {
        new Vector3(0, 0, 0), 
        new Vector3(0, 0, 1), 
        new Vector3(0, 0.08f, 0.9f), 
        new Vector3(0, 0, 1), 
        new Vector3(0, -0.08f, 0.9f)
    };

    public void Init(){
        obj = new GameObject("Line " + id.ToString());
        lineRenderer = obj.AddComponent<LineRenderer>();
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        lineRenderer.positionCount = originalPoints.Length;
        lineRenderer.SetPositions(originalPoints);
    }

    public void Set(Vector3 position, Vector3 direction, float length){
        obj.transform.localPosition = Vector3.zero;
        obj.transform.LookAt(direction, Vector3.up);
        obj.transform.localPosition = position;
        //obj.transform.localEulerAngles = direction;
        obj.transform.localScale = length * Vector3.one;

        Vector3[] points = new Vector3[originalPoints.Length];
        for (int i = 0; i < points.Length; i++){
            points[i] = obj.transform.TransformPoint(originalPoints[i]);
        }
        lineRenderer.SetPositions(points);
    }

    public void Show(){
        lineRenderer.enabled = true;
    }

    public void Hide(){
        lineRenderer.enabled = false;
    }
}