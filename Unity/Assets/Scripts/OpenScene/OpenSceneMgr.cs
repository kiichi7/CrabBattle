using UnityEngine;
using System.Collections;

public class OpenSceneMgr : MonoBehaviour {

    GameManager gm;

    Rect windowrect;

    // Use this for initialization
    void Start () 
    {
        gm = GameManager.GetInstance();

        windowrect = new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 150f, 300f, 300f);
    }

    public void OnGUI()
    {
        windowrect.x = Mathf.Clamp(windowrect.x, 0, Screen.width - windowrect.width);
        windowrect.y = Mathf.Clamp(windowrect.y, 0, Screen.height - windowrect.height);

        windowrect = GUI.Window(1, windowrect, OptionWindow, "Game Type");
    }

    public void OptionWindow(int windowid)
    {
        GUI.Label(new Rect(10, 30, 200, 20), "Single Player");

        if (GUI.Button(new Rect(20, 60, 260, 30), "Play Single Player"))
        {
            gm.isSoloPlay = true;
            Application.LoadLevel("mainscene");            
        }

        GUI.Label(new Rect(10, 100, 200, 20), "Multi Player");

        GUI.Label(new Rect(30, 120, 240, 80), "You'll need to have at least one person running a multiplayer server in order to play multiplayer.  That player will need to forward ports 843 and 14248 to their PC.");

        if (GUI.Button(new Rect(190, 190, 90, 20), "Get Server"))
        {
            Application.OpenURL("http://dodsrv.com/Unity/Crabbattle/CrabBattleServer.exe");
        }

        GUI.Label(new Rect(30, 210, 230, 40), "Server\nIP Address: ");

        gm.ipAddress = GUI.TextField(new Rect(110, 220, 170, 22), gm.ipAddress);

        if (GUI.Button(new Rect(20, 250, 260, 30), "Play Multi Player"))
        {
            gm.isSoloPlay = false;
            Application.LoadLevel("mainscene");
        }

        GUI.DragWindow();
    }
	
    // Update is called once per frame
    void Update () 
    {
	
    }
}
