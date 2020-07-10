using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.SixTen.Scripts;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class SixTenScript : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public TextMesh[] Values;
    public TextMesh[] RGBValues;
    public KMSelectable[] Input;
    public KMSelectable[] Knobs;
    public KMSelectable[] RGBScreens;
    public KMSelectable[] Speed;
    public Image ColorPreview;

    readonly TextMesh[][] hexTexts = new TextMesh[3][]
    {
        new TextMesh[4],
        new TextMesh[4],
        new TextMesh[4]
    };

    Dictionary<RowIdentity, List<string>> hexStrs = new Dictionary<RowIdentity, List<string>>
        {
            { RowIdentity.Red,    new List<string>() },
            { RowIdentity.Green, new List<string>() },
            { RowIdentity.Blue,   new List<string>() }
        };

    bool moduleSolved = false;
    bool fast = false;

    const float slideValue = 0.03137254901960784313725490196078f;
    static int moduleIdCounter = 1;
    int moduleId;
    int verShift;
    Dictionary<RowIdentity, int> horShift = new Dictionary<RowIdentity, int>();

    static readonly RowIdentity[] rowIdentities = { RowIdentity.Red, RowIdentity.Green, RowIdentity.Blue };

    RowIdentity[] rows = { RowIdentity.Red, RowIdentity.Green, RowIdentity.Blue };

    Dictionary<RowIdentity, int[]> valuesInRows = new Dictionary<RowIdentity, int[]>
    {
        { RowIdentity.Red, new int[] { 0, 1, 2, 3 } },
        { RowIdentity.Green, new int[] { 0, 1, 2, 3 } },
        { RowIdentity.Blue, new int[] { 0, 1, 2, 3 } }
    };

    int verShiftDelay;
    readonly Dictionary<RowIdentity, int> horShiftDelay = new Dictionary<RowIdentity, int>();
    readonly Dictionary<RowIdentity, byte[]> solution = new Dictionary<RowIdentity, byte[]>();

    bool[] inputDone = new bool[] { false, false, false, false };

    static Color32 getColor(RowIdentity identity)
    {
        switch (identity)
        {
            case RowIdentity.Red: return new Color32(255, 50, 50, 255);
            case RowIdentity.Green: return new Color32(50, 255, 50, 255);
            case RowIdentity.Blue: return new Color32(75, 75, 255, 255);
            default: throw new InvalidOperationException();
        }
    }

    struct KnobInfo
    {
        public float Value;
        public bool Forwards;
        public bool Slow;
        public Coroutine Coroutine;
    }

    KnobInfo[] knobInfo = new KnobInfo[3];

    Color32[] darkColors = new Color32[] { new Color32(100, 30, 30, 255), new Color32(30, 100, 30, 255), new Color32(30, 30, 100, 255) };
    Color32[] lightColors = new Color32[] { new Color32(255, 100, 100, 255), new Color32(100, 255, 100, 255), new Color32(100, 100, 255, 255) };


    void Start()
    {

        moduleId = moduleIdCounter++;

        for (var i = 0; i < Speed.Length; i++)
        {
            Speed[i].OnInteract += speedPressed(i);
        }
        Speed[1].OnInteract();

        for (var i = 0; i < Input.Length; i++)
            Input[i].OnInteract += tilePressed(i);

        for (var i = 0; i < Knobs.Length; i++)
        {
            knobInfo[i] = new KnobInfo { Value = 0, Slow = false, Forwards = true };
            Knobs[i].OnInteract += knobPressed(i);
            RGBScreens[i].OnInteract += RGBPressed(i);
            Knobs[i].OnInteractEnded += knobReleased(i);
        }
        GetComponentInChildren<ColorWheel>().Setup(getColor(RowIdentity.Red), getColor(RowIdentity.Green), getColor(RowIdentity.Blue), knobInfo[0].Value, knobInfo[1].Value, knobInfo[2].Value, 765);

        verShiftDelay = Random.Range(1, 5);
        verShift = Random.Range(-1, 1);
        if (verShift == 0)
            verShift++;
        var binaryShifted = new Dictionary<RowIdentity, string>();
        foreach (var identity in rowIdentities)
        {
            // Decide on the horizontal shift and delay
            horShift[identity] = Random.Range(-1, 1);
            if (horShift[identity] >= 0)
                horShift[identity]++;
            horShiftDelay[identity] = Random.Range(1, 5);

            // Generate the hex digits and put the important one at the front
            var testHex = new HashSet<string>();
            while (testHex.Count < 4)
                testHex.Add(Random.Range(0, 256).ToString("X2"));
            hexStrs[identity].AddRange(testHex);
            var sortHexStrs = hexStrs[identity].ToList();
            sortHexStrs.Sort();
            var nth = sortHexStrs[verShiftDelay - 1];
            var index = hexStrs[identity].FindIndex(str => str.Equals(nth));
            var hexShifted = hexStrs[identity].GetRange(index, hexStrs[identity].Count - index);
            hexShifted.AddRange(hexStrs[identity].Take(index));
            hexStrs[identity] = hexShifted;

            // Convert to binary and implement the binary shift
            var binary = convertToBinary(Convert.ToUInt32(hexShifted.Join(""), 16));
            var bitShift = mod(-horShift[identity] * horShiftDelay[identity], 32);
            binaryShifted[identity] = binary.Substring(bitShift) + binary.Substring(0, bitShift);
        }

        // Implement the vertical shift and convert to the solution
        for (var identityIx = 0; identityIx < rowIdentities.Length; identityIx++)
            solution[rowIdentities[mod(identityIx + verShift, rowIdentities.Length)]] = Enumerable.Range(0, 4).Select(number => Convert.ToByte(binaryShifted[rowIdentities[identityIx]].Substring(8 * number, 8), 2)).ToArray();

        for (var row = 0; row < 3; row++)
            for (var pos = 0; pos < 4; pos++)
                hexTexts[row][pos] = Values[row * 4 + pos];

        StartCoroutine(shifter());
        StartCoroutine(visualUpdater());
        Debug.LogFormat(@"[SixTen #{0}] Displayed hexadecimal values are: {1}", moduleId, hexStrs.Select(kvp => string.Format(@"{0} = ""{1}""", kvp.Key, kvp.Value.Join(" "))).Join(" - "));
        Debug.LogFormat(@"[SixTen #{0}] Vertical shift: {1}", moduleId, verShift);
        Debug.LogFormat(@"[SixTen #{0}] Horizontal shifts: {1}", moduleId, horShift.Select(kvp => string.Format("{0} = {1}", kvp.Key, kvp.Value)).Join(" | "));
        Debug.LogFormat(@"[SixTen #{0}] Vertical shift delay: {1}", moduleId, verShiftDelay);
        Debug.LogFormat(@"[SixTen #{0}] Horizontal shift delays: {1}", moduleId, horShiftDelay.Select(kvp => string.Format("{0} = {1}", kvp.Key, kvp.Value)).Join(" | "));
        for (var i = 0; i < 4; i++)
            Debug.LogFormat(@"[SixTen #{0}] Input {1} solution: R={2}, G={3}, B={4}", moduleId, i + 1, solution[RowIdentity.Red][i], solution[RowIdentity.Green][i], solution[RowIdentity.Blue][i]);
    }

    private KMSelectable.OnInteractHandler speedPressed(int speed)
    {
        return delegate
        {
            if (moduleSolved)
                return false;
            if (fast)
            {
                if (speed == 1)
                    return false;
                fast = false;
                Speed[speed].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                var arrows = Speed[speed].GetComponentsInChildren<Image>().ToArray();
                foreach (var img in arrows)
                {
                    img.color = new Color32(0, 0, 0, 255);
                }
                Speed[1].GetComponent<MeshRenderer>().material.color = new Color32(0, 0, 0, 255);
                var arrows1 = Speed[1].GetComponentsInChildren<Image>().ToArray();
                foreach (var img in arrows1)
                {
                    img.color = new Color32(255, 255, 255, 255);
                }
            }
            else
            {
                if (speed == 0)
                    return false;
                fast = true;
                Speed[speed].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                var arrows = Speed[speed].GetComponentsInChildren<Image>().ToArray();
                foreach (var img in arrows)
                {
                    img.color = new Color32(0, 0, 0, 255);
                }
                Speed[0].GetComponent<MeshRenderer>().material.color = new Color32(0, 0, 0, 255);
                var arrows1 = Speed[0].GetComponentsInChildren<Image>().ToArray();
                foreach (var img in arrows1)
                {
                    img.color = new Color32(255, 255, 255, 255);
                }
            }
            return false;
        };
    }

    private string convertToBinary(uint value)
    {
        var binaryDigits = new char[32];
        for (var i = 0; i < 32; i++)
            binaryDigits[31 - i] = ((value & (1 << i)) != 0) ? '1' : '0';
        return new string(binaryDigits);
    }

    private Action knobReleased(int knob)
    {
        return delegate ()
        {
            if (knobInfo[knob].Coroutine != null)
                StopCoroutine(knobInfo[knob].Coroutine);
        };
    }

    private KMSelectable.OnInteractHandler RGBPressed(int knob)
    {
        return delegate
        {
            if (moduleSolved)
                return false;

            knobInfo[knob].Forwards = !knobInfo[knob].Forwards;
            Knobs[knob].GetComponent<MeshRenderer>().material.color = knobInfo[knob].Forwards ? lightColors[knob] : darkColors[knob];
            return false;
        };
    }

    private IEnumerator visualUpdater()
    {
        while (true)
        {
            if (moduleSolved)
                break;
            yield return null;
            ColorPreview.color = new Color32((byte) knobInfo[0].Value, (byte) knobInfo[1].Value, (byte) knobInfo[2].Value, 255);
            GetComponentInChildren<ColorWheel>().ColorWheelUpdate(knobInfo[0].Value, knobInfo[1].Value, knobInfo[2].Value, 765 - knobInfo.Select(knob => (float) knob.Value).ToArray().Sum());
        }
    }

    private KMSelectable.OnInteractHandler knobPressed(int knob)
    {
        return delegate
        {
            if (moduleSolved)
                return false;
            knobInfo[knob].Coroutine = StartCoroutine(Slide(knob));
            return false;
        };
    }

    private IEnumerator Slide(int knob)
    {
        var z = new[] { 150, 75, 0 };
        knobInfo[knob].Value += knobInfo[knob].Forwards ? 1 : -1;
        do
        {
            knobInfo[knob].Value = Math.Min(255, Math.Max(0, knobInfo[knob].Value));
            Knobs[knob].transform.localPosition = new Vector3(1 + knobInfo[knob].Value / 255 * 8, 0.13f, z[knob]);
            RGBValues[knob].text = ((int) knobInfo[knob].Value).ToString();
            yield return null;
            knobInfo[knob].Value += (knobInfo[knob].Forwards ? 1 : -1) * ((fast ? 100 : 10) * Time.deltaTime);
        }
        while (true);
    }

    KMSelectable.OnInteractHandler tilePressed(int pos)
    {
        return delegate
        {
            if (moduleSolved)
                return false;

            var curSliderValues = knobInfo.Select(kn => kn.Value).ToArray();
            var expectedValues = rowIdentities.Select(id => solution[id][pos]).ToArray();
            if (!curSliderValues.Select(v => (byte) v).SequenceEqual(expectedValues))
            {
                Module.HandleStrike();
                Debug.LogFormat(@"[SixTen #{0}] Wrong color for input {1}! {2} was entered.", moduleId, pos + 1, knobInfo.Select(v => v.Value).ToArray().Join(", "));
                return false;
            }
            else
            {
                Input[pos].GetComponent<MeshRenderer>().material.color = new Color32((byte) curSliderValues[0], (byte) curSliderValues[1], (byte) curSliderValues[2], 255);
                inputDone[pos] = true;
                if (inputDone.SequenceEqual(new bool[] { true, true, true, true }))
                {
                    StopAllCoroutines();
                    Module.HandlePass();
                    moduleSolved = true;
                }
            }
            return false;
        };
    }

    IEnumerator shifter()
    {
        var secondsToWaitPerRow = new Dictionary<RowIdentity, int>();
        foreach (var identity in rowIdentities)
            secondsToWaitPerRow[identity] = horShiftDelay[identity];
        var secondsToWait = verShiftDelay;

        while (true)
        {
            secondsToWait--;
            if (secondsToWait == 0)
            {
                var newRows = new RowIdentity[3];
                for (var i = 0; i < 3; i++)
                    newRows[mod(i + verShift, 3)] = rows[i];
                rows = newRows;
                secondsToWait = verShiftDelay;
            }

            foreach (var identity in rowIdentities)
            {
                secondsToWaitPerRow[identity]--;
                if (secondsToWaitPerRow[identity] == 0)
                {
                    var newValues = new int[4];
                    for (var i = 0; i < 4; i++)
                        newValues[mod(i + horShift[identity], 4)] = valuesInRows[identity][i];
                    valuesInRows[identity] = newValues;
                    secondsToWaitPerRow[identity] = horShiftDelay[identity];
                }
            }

            foreach (var identity in rowIdentities)
            {
                var row = Array.IndexOf(rows, identity);
                for (var pos = 0; pos < 4; pos++)
                {
                    hexTexts[row][pos].text = hexStrs[identity][valuesInRows[identity][pos]];
                    hexTexts[row][pos].color = getColor(identity);
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    string ToHexString(char ch)
    {
        return ((int) ch).ToString("X2");
    }

    int mod(int x, int m)
    {
        return (x % m + m) % m;
    }

#pragma warning disable 0414
    readonly string TwitchHelpMessage = @"!{0} 151 255 178, 134 67 78 [Set these RGB values and input them]";
#pragma warning restore 0414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (moduleSolved)
            yield break;

        Match m;

        if ((m = Regex.Match(command, @"^\s*([0-9, ]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var values = m.Groups[1].Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(str =>
            {
                int val;
                return int.TryParse(str, out val) ? val : (int?) null;
            }).ToArray();
            if (values.Any(v => v == null || v.Value < 0 || v.Value > 255))
            {
                yield return "sendtochaterror The values must be 0–255.";
                yield break;
            }
            if (values.Length % 3 != 0)
            {
                yield return "sendtochaterror Always provide values in groups of three (R, G, and B).";
                yield break;
            }
            yield return null;
            for (var i = 0; i < values.Length / 3; i++)
            {
                for (var knobIx = 0; knobIx < 3; knobIx++)
                {
                    if (knobInfo[knobIx].Value > values[3 * i + knobIx].Value && knobInfo[knobIx].Forwards)
                        RGBScreens[knobIx].OnInteract();
                    else if (knobInfo[knobIx].Value < values[3 * i + knobIx].Value && !knobInfo[knobIx].Forwards)
                        RGBScreens[knobIx].OnInteract();
                    yield return new WaitForSeconds(.1f);
                    Knobs[knobIx].OnInteract();
                    yield return new WaitUntil(() => knobInfo[knobIx].Value == values[3 * i + knobIx].Value);
                    Knobs[knobIx].OnInteractEnded();
                }
                var inputIx = Array.IndexOf(inputDone, false);
                Input[inputIx].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
            yield break;

        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat(@"[SixTen #{0}] Module was force-solved by TP!", moduleId);

        for (var i = 0; i < inputDone.Length; i++)
        {
            if (inputDone[i])
                continue;
            for (var j = 0; j < 3; j++)
            {
                if (Mathf.Abs(knobInfo[j].Value - solution[rowIdentities[j]][i]) < 5f)
                    Speed[0].OnInteract();
                else
                    Speed[1].OnInteract();
                yield return new WaitForSeconds(.1f);
                if (knobInfo[j].Value > solution[rowIdentities[j]][i] && knobInfo[j].Forwards)
                {
                    RGBScreens[j].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                else if (knobInfo[j].Value < solution[rowIdentities[j]][i] && !knobInfo[j].Forwards)
                {
                    RGBScreens[j].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                Knobs[j].OnInteract();

                while (true)
                {
                    yield return null;
                    if (Mathf.Abs(knobInfo[j].Value - solution[rowIdentities[j]][i]) < 5f)
                        Speed[0].OnInteract();
                    if ((byte) knobInfo[j].Value == solution[rowIdentities[j]][i])
                        break;
                }
                Knobs[j].OnInteractEnded();
                yield return new WaitForSeconds(.1f);
            }
            Input[i].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }
}
