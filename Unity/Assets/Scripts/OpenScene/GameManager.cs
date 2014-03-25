using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour {

    public static GameManager Instance;
    public static GameObject Container;

    public bool isSoloPlay = true;
    public string ipAddress = "127.0.0.1";

    public static GameManager GetInstance()
    {
        if (!Instance)
        {
            Container = new GameObject();
            Container.name = "GameManager";
            Instance = Container.AddComponent(typeof(GameManager)) as GameManager;
        }
        return Instance;
    }

	// Use this for initialization
	void Start () 
    {
        DontDestroyOnLoad(this);
	}
	
	// Update is called once per frame
	void Update () 
    {
	
	}
}
