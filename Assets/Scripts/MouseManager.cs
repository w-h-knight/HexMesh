using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseManager : MonoBehaviour {

    public GameObject HexMapGO;

    private int xSlowSensitivity = 50;
    private int xSlowSpeed = 1;
    private int xFastSensitivity = 10;
    private int xFastSpeed = 10;
    private int ySlowSensitivity = 50;
    private int ySlowSpeed = 1;
    private int yFastSensitivity = 10;
    private int yFastSpeed = 10;
    private HexMap hexmap;

    // Use this for initialization
    void Start() {
        hexmap = HexMapGO.GetComponent<HexMap>();
    }

    // Update is called once per frame
    void Update() {
        //if mouse is outside of screen just return
        Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);
        if(!screenRect.Contains(Input.mousePosition)) { return; }

        //move camera up and down
        if(Input.mouseScrollDelta.y != 0.0) { DollyCamera(Input.mouseScrollDelta.y); }

        // Check if on the left edge
        if(Input.mousePosition.x <= xSlowSensitivity) {
            if(Input.mousePosition.x <= xFastSensitivity) {
                Vector3 pan_distance = Vector3.right * Time.deltaTime * xFastSpeed;
                hexmap.UpdatePanDistance(pan_distance);
                PanMap(pan_distance);
            } 
            else {
                Vector3 pan_distance = Vector3.right * Time.deltaTime * xSlowSpeed;
                hexmap.UpdatePanDistance(pan_distance);
                PanMap(pan_distance);
            }
        }

        // Check if on the right edge
        if(Input.mousePosition.x >= Screen.width - xSlowSensitivity) {
            if(Input.mousePosition.x >= Screen.width - xFastSensitivity) {
                Vector3 pan_distance = Vector3.left * Time.deltaTime * xFastSpeed;
                hexmap.UpdatePanDistance(pan_distance);
                PanMap(pan_distance);
            } 
            else {
                Vector3 pan_distance = Vector3.left * Time.deltaTime * xSlowSpeed;
                hexmap.UpdatePanDistance(pan_distance);
                PanMap(pan_distance); }
        }

        // Check if on top edge
        if(Input.mousePosition.y >= Screen.height - ySlowSensitivity) {
            if(Input.mousePosition.y >= Screen.height - yFastSensitivity) 
                { PanMap(Vector3.back * Time.deltaTime * yFastSpeed); } 
            else { PanMap(Vector3.back * Time.deltaTime * ySlowSpeed); }
        }

        // Check if on bottom edge
        if(Input.mousePosition.y <= ySlowSensitivity) {
            if(Input.mousePosition.y <= yFastSensitivity) 
                { PanMap(Vector3.forward * Time.deltaTime * yFastSpeed); } 
            else { PanMap(Vector3.forward * Time.deltaTime * ySlowSpeed); }
        }
    }
    public void DollyCamera(float yOffset) {
        Camera.main.transform.position -= Vector3.down;
    }

    public void PanMap(Vector3 panVector) {
        HexMapGO.transform.Translate(panVector);
    }
}
