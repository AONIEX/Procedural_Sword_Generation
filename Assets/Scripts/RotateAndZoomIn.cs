using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAndZoomIn : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 200f;
    public bool invertX = false;
    public bool invertY = false;

    [Header("Zoom Settings")]
    public Camera targetCamera;
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 20f;

    private Vector3 lastMousePos;

    void Update()
    {
        HandleRotation();
        HandleZoom();
        HandleArrowMovement();
    }

    void HandleRotation()
    {
        if (Input.GetMouseButtonDown(1))
        {
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;

            float rotX = (invertY ? delta.y : -delta.y) * rotationSpeed * Time.deltaTime;
            float rotY = (invertX ? -delta.x : delta.x) * rotationSpeed * Time.deltaTime;

            transform.Rotate(rotX, rotY, 0, Space.World);
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            Vector3 pos = transform.localPosition;

            // Move object along Y axis
            pos.z += scroll * zoomSpeed;

            // Clamp Y movement
            pos.z = Mathf.Clamp(pos.z, minZoom, maxZoom);

            transform.localPosition = pos;
        }
    }

    void HandleArrowMovement()
    {
        float horizontal = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            horizontal = 1f;

        if (Input.GetKey(KeyCode.RightArrow))
            horizontal = -1f;

        if (horizontal != 0f)
        {
            Vector3 pos = transform.localPosition;
            pos.x += horizontal * zoomSpeed * Time.deltaTime; // reuse zoomSpeed as move speed
            transform.localPosition = pos;
        }
    }
}