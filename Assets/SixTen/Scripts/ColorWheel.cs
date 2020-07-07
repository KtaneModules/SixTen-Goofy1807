using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ColorWheel : MonoBehaviour
{

    public Image WedgePrefab;
    Image[] Wedges = new Image[4];

    public void Setup(Color32 red, Color32 green, Color32 blue, float redValue, float greenValue, float blueValue, float blackValue)
    {

        var colorValues = new[] { redValue, greenValue, blueValue, blackValue};
        var colors = new[] { red, green, blue, new Color32(0,0,0,0) };
        var zRot = 0f;

        for (var i = 0; i < Wedges.Length; i++)
        {
            Wedges[i] = Instantiate(WedgePrefab) as Image;
            Wedges[i].transform.SetParent(transform, false);
            Wedges[i].color = colors[i];
            Wedges[i].fillAmount =  colorValues[i] / colorValues.Sum();
            Wedges[i].transform.localRotation = Quaternion.Euler(new Vector3(180f, 0f, zRot));
            zRot += Wedges[i].fillAmount * 360f;
        }


    }

    public void ColorWheelUpdate(float redValue, float greenValue, float blueValue, float blackValue)
    {
        var zRot = 0f;
        var colorValues = new[] { redValue, greenValue, blueValue, blackValue };
        for (var i = 0; i < Wedges.Length; i++)
        {
            Wedges[i].fillAmount = colorValues[i] / colorValues.Sum();
            Wedges[i].transform.localRotation = Quaternion.Euler(new Vector3(180f, 0f, zRot));
            zRot += Wedges[i].fillAmount * 360f;
        }

    }

}
