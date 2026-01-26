using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum MeshQuality
{
    Low,
    Medium,
    High,
    Ultra
}

public enum BladeBaseProfile
{
    Lenticular,     // oval
    Diamond,        // four planes, central ridge
    HollowGround,   // concave bevels
    Hexagonal,      // flat spine + angled sides
    Triangular,     // stiletto
    Flat            // rectangular
}

public class BladeEnums : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
