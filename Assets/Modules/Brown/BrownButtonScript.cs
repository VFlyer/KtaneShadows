using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BrownButton;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class BrownButtonScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable BrownButtonSelectable;
    public GameObject BrownButtonCap;
    public MeshRenderer WideMazeScreen;
    public Camera WideMazeCamera;
    public Transform WallsParent;
    public GameObject WallTemplate;
    public GameObject Camera;
    public Material[] Materials;

    public KMSelectable[] OtherButtons;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _moduleSolved;
    private bool _moduleActivated = false;
    List<Vector3Int> _chosenNet;
    private Vector3 _currentRotation;
    private Vector3Int _currentPosition;
    private Coroutine _moveRoutine = null;
    Dictionary<Vector3Int, Ax> _absoluteAxes;
    private Ax _correctAxis;
    private Quaternion _trueRotation = Quaternion.identity;

    private const int REPEATSCOUNT = 0;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        BrownButtonSelectable.OnInteract += BrownButtonPress;
        for(int i = 0; i < OtherButtons.Length; ++i)
        {
            int j = i;
            OtherButtons[i].OnInteract += () => { OtherPress(j); return false; };
        }
        BrownButtonSelectable.OnInteractEnded += BrownButtonRelease;
        WideMazeScreen.material.mainTexture = new RenderTexture(WideMazeScreen.material.mainTexture as RenderTexture);
        WideMazeCamera.targetTexture = WideMazeScreen.material.mainTexture as RenderTexture;

        panic:

        List<Vector3Int> changes = new List<Vector3Int> { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
        _chosenNet = new List<Vector3Int> { new Vector3Int(0, 0, 0) };
        List<Or> searchSpace = new List<Or>();
        Ax[] orig = new Ax[8] { Ax.Left, Ax.Front, Ax.Back, Ax.Right, Ax.Up, Ax.Zig, Ax.Down, Ax.Zag };
        _absoluteAxes = new Dictionary<Vector3Int, Ax> { { new Vector3Int(0, 0, 0), Ax.Down } };

        foreach(Vector3Int change in changes)
            searchSpace.Add(new Or { Pos = change, Axes = orig.RotateFromChange(change) });

        List<Ax> claimed = new List<Ax> { Ax.Down };
        int repeatCount = 0;

        while(claimed.Count < 8)
        {
            IEnumerable<Or> allOrs = searchSpace.Where(o => o.Axes != null && !claimed.Contains(o.Axes[6]) && !_chosenNet.Contains(o.Pos));
            if(allOrs.Count() == 0)
            {
                Debug.LogFormat("<Brown's Shadow #{0}> Panicked!", _moduleId);
                goto panic;
            }
            Or newOr = allOrs.PickRandom();
            _chosenNet.Add(newOr.Pos);
            _absoluteAxes.Add(newOr.Pos, newOr.Axes[6]);
            claimed.Add(newOr.Axes[6]);

            foreach(Vector3Int change in changes)
            {
                Or created = new Or { Pos = newOr.Pos + change, Axes = newOr.Axes.RotateFromChange(change) };

                foreach(Or or in searchSpace.Where(o => o.Pos == created.Pos))
                    or.Axes = null;

                if(!searchSpace.Any(o => o.Pos == created.Pos))
                    searchSpace.Add(created);
            }

            if(changes.Count(c => _chosenNet.Contains(newOr.Pos + c)) >= 5)
            {
                foreach(Vector3Int change in changes)
                {
                    foreach(Or or in searchSpace.Where(o => o.Pos == newOr.Pos + change))
                        or.Axes = null;
                }
            }

            foreach(Or or in searchSpace.Where(o => o.Pos == newOr.Pos))
                or.Axes = null;

            if(claimed.Count == 8 && repeatCount++ < REPEATSCOUNT)
                claimed = new List<Ax>();
        }

        int ansAx = Rnd.Range(0, 4);
        Debug.LogFormat("<Brown's Shadow #{0}> The solution axis is id {1}.", _moduleId, ansAx);

        List<int> ixes = new List<int> { 1, 9, 17 }.Shuffle();
        ixes.Insert(ansAx, 0);

        List<Ax> markedAxes = new List<Ax>();

        List<Ax[]> alltests = new List<Ax[]> {
            new Ax[] { Ax.Up, Ax.Down },
            new Ax[] { Ax.Left, Ax.Right },
            new Ax[] { Ax.Front, Ax.Back },
            new Ax[] { Ax.Zig, Ax.Zag }
        };

        Debug.LogFormat("[Brown's Shadow #{0}] Chose net: {1}", _moduleId, _chosenNet.Select(v => v.ToString()).Join(", "));
        for(int ix = 0; ix < _chosenNet.Count; ix++)
            Debug.LogFormat("[Brown's Shadow #{0}] Cube {1} corresponds to {2}.", _moduleId, _chosenNet[ix], (int)_absoluteAxes[_chosenNet[ix]]);

        for(int i = 0; i < alltests.Count; i++)
        {
            Ax[] tests = alltests[i];
            IEnumerable<Vector3Int> nodes = _chosenNet.Where(p => _absoluteAxes[p] == tests[0] || _absoluteAxes[p] == tests[1]);
            Vector3Int marked = nodes.PickRandom();
            if(ansAx != i)
                markedAxes.Add(_absoluteAxes[marked]);
            Vector3Int markedWall = changes.Where(c => !_chosenNet.Contains(marked + c)).PickRandom();

            AddWall(marked, markedWall, ansAx == i ? Materials[0] : Materials[ixes[i] + (int)_absoluteAxes[marked]]);
            if(ansAx != i)
            {
                Debug.LogFormat("[Brown's Shadow #{0}] Cube {1} is displaying {2}.", _moduleId, marked, Materials[ixes[i] + (int)_absoluteAxes[marked]].name.Substring(7));
            }

            foreach(Vector3Int change in changes.Where(c => !_chosenNet.Contains(marked + c) && c != markedWall))
                AddWall(marked, change, Materials[0]);

            foreach(Vector3Int pos in nodes.Where(n => n != marked))
            {
                foreach(Vector3Int change in changes.Where(c => !_chosenNet.Contains(pos + c)))
                    AddWall(pos, change, Materials[0]);
            }
        }

        if(alltests.Select(a => a[0]).Count(a => markedAxes.Contains(a)) >= 2)
            _correctAxis = alltests[ansAx][0];
        else
            _correctAxis = alltests[ansAx][1];

        Debug.LogFormat("[Brown's Shadow #{0}] The correct cell to submit is {1}.", _moduleId, _chosenNet.First(c => _absoluteAxes[c] == _correctAxis));

        _currentPosition = _chosenNet.PickRandom();
        Vector3 end = new Vector3(_currentPosition.x, _currentPosition.y, _currentPosition.z) * -0.1f - _currentRotation * 0.1f;
        WallsParent.localPosition = end;
        _currentRotation = Vector3Int.down;

        GetComponentInChildren<CameraScript>().UpdateChildren();
        Module.OnActivate += delegate
        {
            _moduleActivated = true;
        };
    }

    private void OtherPress(int j)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OtherButtons[j].transform);
        OtherButtons[j].AddInteractionPunch();

        switch(j)
        {
            case 4:
                _trueRotation *= Quaternion.Euler(0f, 0f, 90f);
                break;
            case 0:
                _trueRotation *= Quaternion.Euler(0f, -90f, 0f);
                break;
            case 1:
                _trueRotation *= Quaternion.Euler(0f, 0f, -90f);
                break;
            case 3:
                _trueRotation *= Quaternion.Euler(90f, 0f, 0f);
                break;
            case 2:
                _trueRotation *= Quaternion.Euler(0f, 90f, 0f);
                break;
            case 5:
                _trueRotation *= Quaternion.Euler(-90f, 0f, 0f);
                break;
        }

        StartCoroutine(RotateCamera());
    }

    private class Or
    {
        public Ax[] Axes;
        public Vector3Int Pos;
    }

    public enum Ax
    {
        Left,
        Front,
        Back,
        Right,
        Up,
        Zig,
        Down,
        Zag
    }

    private const float DELAY_A = 0.5f;
    private int _cameraPresses = 0;

    private IEnumerator RotateCamera()
    {
        Vector3 pointing = _trueRotation * Vector3.down;
        int presses = ++_cameraPresses;
        if(pointing.x > 0.75f)
            _currentRotation = new Vector3(1f, 0f, 0f);
        else if(pointing.x < -0.75f)
            _currentRotation = new Vector3(-1f, 0f, 0f);
        else if(pointing.y > 0.75f)
            _currentRotation = new Vector3(0f, 1f, 0f);
        else if(pointing.y < -0.75f)
            _currentRotation = new Vector3(0f, -1f, 0f);
        else if(pointing.z > 0.75f)
            _currentRotation = new Vector3(0f, 0f, 1f);
        else if(pointing.z < -0.75f)
            _currentRotation = new Vector3(0f, 0f, -1f);

        Quaternion start = Camera.transform.localRotation;
        float startTime = Time.time;
        while(startTime + DELAY_A > Time.time)
        {
            yield return null;
            if(presses != _cameraPresses)
                yield break;
            Camera.transform.localRotation = Quaternion.Slerp(start, _trueRotation, (Time.time - startTime) / DELAY_A);
        }
        Camera.transform.localRotation = _trueRotation;
    }

    private IEnumerator MoveMaze()
    {
        Vector3 start = WallsParent.localPosition;
        Vector3 end = new Vector3(_currentPosition.x, _currentPosition.y, _currentPosition.z) * -0.1f - _currentRotation * 0.1f;
        float startTime = Time.time;
        while(startTime + DELAY_A > Time.time)
        {
            yield return null;
            WallsParent.localPosition = Vector3.Lerp(start, end, (Time.time - startTime) / DELAY_A);
        }
        WallsParent.localPosition = end;
    }

    private void AddWall(Vector3Int position, Vector3Int direction, Material m)
    {
        GameObject go = Instantiate(WallTemplate, WallsParent);
        go.transform.localPosition = position;
        Vector3 rot = DirectionToEuler(direction);
        go.transform.localEulerAngles = rot;
        go.transform.localScale = new Vector3(1f, 1f, 1f);
        go.GetComponentInChildren<CustomMaterialInfo>().Color = m;
    }

    private Vector3 DirectionToEuler(Vector3Int direction)
    {
        Vector3 rot = new Vector3(0f, 0f, 0f);
        if(direction.x == 1)
            rot = new Vector3(0f, 0f, 90f);
        if(direction.x == -1)
            rot = new Vector3(0f, 0f, -90f);
        if(direction.y == 1)
            rot = new Vector3(180f, 0f, 0f);
        if(direction.y == -1)
            rot = new Vector3(0f, 0f, 0f);
        if(direction.z == 1)
            rot = new Vector3(-90f, 0f, 0f);
        if(direction.z == -1)
            rot = new Vector3(90f, 0f, 0f);
        return rot;
    }

    private bool BrownButtonPress()
    {
        StartCoroutine(AnimateButton(0f, -0.05f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        if(_moduleActivated && !_moduleSolved)
        {
            if(_chosenNet.Any(t => t == _currentPosition + _currentRotation))
            {
                if(_moveRoutine != null)
                    StopCoroutine(_moveRoutine);
                _moveRoutine = StartCoroutine(MoveMaze());
                _currentPosition += new Vector3Int((int)_currentRotation.x, (int)_currentRotation.y, (int)_currentRotation.z);
            }
            else
            {
                if(_absoluteAxes[_currentPosition] == _correctAxis)
                {
                    Debug.LogFormat("[Brown's Revenge #{0}] You submitted correctly. Good job!", _moduleId);
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    Module.HandlePass();
                    _moduleSolved = true;
                    StartCoroutine(FadeScreen());
                }
                else
                {
                    Debug.LogFormat("[Brown's Revenge #{0}] You tried to submit at {1}. That is incorrect. Strike!", _moduleId, _currentPosition);
                    Module.HandleStrike();
                }
            }
        }
        return false;
    }

    private IEnumerator FadeScreen()
    {
        float time = Time.time;
        while(Time.time - time < 5f)
        {
            WideMazeScreen.material.color = Color.Lerp(new Color(1f, 1f, 1f), new Color(0f, 0f, 0f), (Time.time - time) / 5f);
            yield return null;
        }
        WideMazeScreen.material.color = new Color(0f, 0f, 0f);
    }

    private void BrownButtonRelease()
    {
        StartCoroutine(AnimateButton(-0.05f, 0f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
    }

    private IEnumerator AnimateButton(float a, float b)
    {
        var duration = 0.1f;
        var elapsed = 0f;
        while(elapsed < duration)
        {
            BrownButtonCap.transform.localPosition = new Vector3(0f, Easing.InOutQuad(elapsed, a, b, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        BrownButtonCap.transform.localPosition = new Vector3(0f, b, 0f);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = @"Use ""!{0} F"" to press the brown button. Use ""!{0} QWEASD"" to press every other button.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = Regex.Replace(command.ToLowerInvariant(), @"\s+", "");
        if(command.Length == 0 || command.Any(c => !"fqweasd".Contains(c)))
            yield break;
        yield return null;
        foreach(char c in command)
        {
            if(c == 'f')
                BrownButtonSelectable.OnInteract();
            else if(c == 'q')
                OtherButtons[0].OnInteract();
            else if(c == 'w')
                OtherButtons[5].OnInteract();
            else if(c == 'e')
                OtherButtons[2].OnInteract();
            else if(c == 'a')
                OtherButtons[1].OnInteract();
            else if(c == 's')
                OtherButtons[3].OnInteract();
            else if(c == 'd')
                OtherButtons[4].OnInteract();

            yield return new WaitForSeconds(0.1f);

            if(c == 'f')
            {
                BrownButtonSelectable.OnInteractEnded();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
