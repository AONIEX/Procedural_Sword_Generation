using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum SwordColors
{
    Iron = 0,
    Bronze = 1,
    Steel = 2,
    Carbon = 3,
    Damascus = 4, 
    Wootz= 5,
    Obsidian = 6
}

public enum SwordEffects
{
    Blood,
    Mud,
    Rust,
    Oxidation
}

[SerializeField]
public class ShaderParamaters
{
    [DisplayName("Metal Type", "Texture", 2, "")]
    public SwordColors swordColor = SwordColors.Iron;

    [DisplayName("Effect Type", "Texture", 2, "")]
    public SwordEffects swordEffcet = SwordEffects.Rust;

    [Range(0.1f,0.9f)][DisplayName("Effect Amount", "Texture", 3, "")]
    public float effectAmount = 1.0f;
}

public class SwordShaderControl : MonoBehaviour
{
    [DisplayName("Metal Type", "Texture", 2, "")]
    public ShaderParamaters swordParamaters = new ShaderParamaters();
    public Color swordLightColor;
    public Color swordDarkColor;
    public Color swordScratchColor;
    
    public Material material;

    private SwordColors oldSwordColor = SwordColors.Iron;
    private SwordEffects oldEffect = SwordEffects.Rust;
    private float oldEffectAmount = 0.0f;

    public Color lightRustColor;
    public Color darkRustColor;

    public Color lightBloodColor;
    public Color darkBloodColor;

    public Color lightMudColor;
    public Color darkMudColor;

    public Color lightOxidiationColor;
    public Color darkOxidiationColor;


    // Start is called before the first frame update
    void Start()
    {
        swordParamaters.swordColor = SwordColors.Iron;
        swordParamaters.swordEffcet = SwordEffects.Rust;

        swordLightColor = new Color(0.7509433f, 0.7509433f, 0.7509433f, 1f);
        swordDarkColor = new Color(0.6603774f, 0.6603774f, 0.6603774f, 1f);
        swordScratchColor = new Color(0.6603774f, 0.6603774f, 0.6603774f, 1f);
        oldSwordColor = swordParamaters.swordColor;
        oldEffect = swordParamaters.swordEffcet;
    }

    // Update is called once per frame
    void Update()
    {
        if (oldSwordColor != swordParamaters.swordColor || oldEffect != swordParamaters.swordEffcet || swordParamaters.effectAmount != oldEffectAmount)
        {
            UpdateShader();
        }

        if (Input.GetKeyUp(KeyCode.Space)) { 
            UpdateShader();
        }

    }

    public void UpdateShader()
    {

        UpdateColors();
        UpatesEffects();

        if (material == null)
            material = GetComponent<Renderer>().material;


        material.SetColor("_LightestBaseColor", swordLightColor);
        material.SetColor("_DarkestBaseColor", swordDarkColor);
        material.SetColor("_Scratch_Color", swordScratchColor);

    }

    public void UpdateColors()
    {
        switch (swordParamaters.swordColor)
        {
            case SwordColors.Iron:
                swordLightColor = new Color(0.7509433f, 0.7509433f, 0.7509433f, 1f);
                swordDarkColor = new Color(0.6603774f, 0.6603774f, 0.6603774f, 1f);
                swordScratchColor = new Color(0.8264151f, 0.8264151f, 0.8264151f, 1f);

                break;
            case SwordColors.Bronze:
                swordLightColor= new Color(0.6603774f, 0.5177893f, 0.3625845f, 1f);
                swordDarkColor = new Color(0.5686274f, 0.3950574f, 0.2784314f, 1f);
                swordScratchColor = new Color(0.9018868f, 0.9018868f, 0.9018868f, 1);

                break;
            case SwordColors.Steel:
                swordLightColor = new Color(0.8509804f, 0.8509804f, 0.8509804f, 1f);
                swordDarkColor = new Color(0.4784313f, 0.4784313f, 0.4784313f, 1f);
                swordScratchColor = new Color(1, 1, 1, 1);

                break;
            case SwordColors.Carbon:
                swordLightColor = new Color(0.7098039f, 0.7098039f, 0.7098039f, 1f);
                swordDarkColor = new Color(0.1803921f, 0.1843137f, 0.2f, 1f);
                swordScratchColor = new Color(1, 1, 1, 1);

                break;
            case SwordColors.Damascus:
                swordLightColor = new Color(0.7843137f, 0.7843137f, 0.7843137f, 1f);
                swordDarkColor = new Color(0.3529412f, 0.3529412f, 0.3529412f, 1f);
                swordScratchColor = new Color(1, 1, 1, 1);

                break;
            case SwordColors.Wootz:
                swordLightColor = new Color(0.654902f, 0.654902f, 0.654902f, 1f);
                swordDarkColor = new Color(0.2470588f, 0.2470588f, 0.2470588f, 1f);
                swordScratchColor = new Color(1, 1, 1, 1);
                break;
            case SwordColors.Obsidian:
                swordLightColor = new Color(0.2274509f, 0.2470588f, 0.3607843f, 1f);
                swordDarkColor = new Color(0.03921569f, 0.03921569f, 0.0470588f, 1f);
                swordScratchColor = new Color(1, 1, 1, 1);


                break;
            default:
                break;
        }

        if (swordParamaters.swordColor == SwordColors.Wootz)
        {
            material.SetInt("_useWootzPattern", 1);
        }
        else
        {
            material.SetInt("_useWootzPattern", 0);

        }

        oldSwordColor = swordParamaters.swordColor;
    }


    public void UpatesEffects()
    {

        switch (swordParamaters.swordEffcet)    
        {
            case SwordEffects.Blood:
                material.SetColor("_Rust_color", lightBloodColor);
                material.SetColor("_Dark_Rust_Color", darkBloodColor);
                break;
            case SwordEffects.Mud:
                material.SetColor("_Rust_color", lightMudColor);
                material.SetColor("_Dark_Rust_Color", darkMudColor);
                break;
            case SwordEffects.Rust:
                material.SetColor("_Rust_color", lightRustColor);
                material.SetColor("_Dark_Rust_Color", darkRustColor);
                break;
            case SwordEffects.Oxidation:
                material.SetColor("_Rust_color", lightOxidiationColor);
                material.SetColor("_Dark_Rust_Color", darkOxidiationColor);
                break;
            default:
                break;
        }

        material.SetFloat("_Rust_Power", swordParamaters.effectAmount);
        oldEffectAmount = swordParamaters.effectAmount;
        oldEffect = swordParamaters.swordEffcet;


    }

    public ShaderParamaters GetData() { 
    
        return swordParamaters;
    }

    public void ApplyData(ShaderParamaters shaderParamaters) {
        this.swordParamaters = shaderParamaters;

        UpdateShader();
    }
}
