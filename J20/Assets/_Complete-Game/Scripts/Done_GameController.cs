using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using System;
using System.Text;

public class Done_GameController : MonoBehaviour
{
    public GameObject[] hazards;

    private float spawnWait = 1;
    private float startWait = 2;
    private float waveWait = 2;
    public int m_LocalNumber;
    private Dictionary<int, Done_PlayerController> m_Players;
    private System.Object thisLock = new System.Object();
    [HideInInspector]
    public int PlayerNums;

    public GUIText scoreText;
    public GUIText restartText;
    public GUIText gameOverText;
    public GameObject m_PlayerPrefab;
    //生成陨石的位置
    private Vector3 spawnValues;
    private int hazardCount = 4;

    //创建游戏对象
    private Queue<Message> m_ConstructQueue = new Queue<Message>();
    //产生陨石对象
    private Queue<Message> m_ConstructWavesQueue = new Queue<Message>();

    private bool gameOver;
    private bool restart;
    private int score;
    private bool threadRun = false;
    private NetworkStream m_stream;
    //private TcpClient cln;

    void Awake()
    {
        m_Players = new Dictionary<int, Done_PlayerController>();
        PlayerNums = 0;
        spawnValues = new Vector3(6, 0, 16);
        gameOver = false;
        restart = false;
        restartText.text = "";
        gameOverText.text = "";
        score = 0;
        UpdateScore ();

        //StartCoroutine (SpawnWaves ());

        //创建无参线程对象
        if (!threadRun)
        {
            threadRun = true;
            Thread thr = new Thread(Func);
            //启动线程
            thr.Start();
        }

    }

    void Update()
    {
        if (gameOver)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                gameOverText.text = "";
                restartText.text = "";
                Message reset = new Message();
                reset.id = -1;
                reset.type = 4;
                Send(reset);
                //
            }
        }

        if (restart)
        {
            restart = false;
            gameOver = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            //后面不会执行
        }
        if (m_ConstructQueue.Count == 0)
            return;

        //Debug.Log("msg checked!");
        var msg = m_ConstructQueue.Dequeue();
        SpawnPlayers(msg.id, new Vector3(0, 0, 0));   
    }

    void FixedUpdate()
    {
        //make rocks
         if (m_ConstructWavesQueue.Count == 0)
            return;
        //Debug.Log("msg checked!");
        var msg = m_ConstructWavesQueue.Dequeue();
        SpawnWaves(msg.id, msg.h);
    }
    void SpawnPlayers(int id, Vector3 position)
    {
        GameObject instance = Instantiate(m_PlayerPrefab, position, Quaternion.Euler(0f, 0f, 0f)) as GameObject;
        Done_PlayerController tmp = instance.GetComponent<Done_PlayerController>();
        tmp.playerid = id;
        tmp.localplayerid = m_LocalNumber;
        tmp.m_sender = this;
        tmp.m_MsgQueue = new Queue<Message>();
        m_Players[id] = tmp;
        PlayerNums++;
        Debug.Log("Player:" + Convert.ToString(id) + " construct");
    }

    void SpawnWaves(int id, float x)
    {
        GameObject hazard = hazards[id];
        Vector3 spawnPosition = new Vector3(x, spawnValues.y, spawnValues.z);
        Quaternion spawnRotation = Quaternion.identity;
        Instantiate(hazard, spawnPosition, spawnRotation);
    }

    public void AddScore(int newScoreValue)
    {
        score += newScoreValue;
        UpdateScore();
    }

    void UpdateScore()
    {
        scoreText.text = "Score: " + score;
    }

    public void GameOver()
    {
        gameOverText.text = "Game Over!";
        restartText.text = "Press 'R' for Restart";
        gameOver = true;
    }

    public void OnClicked()
    {
        threadRun = false;
    }

    public void Send(Message msg)
    {
        string sendmsg = msg.SaveToString() + "#";
        //直接将msg发送出去
        ASCIIEncoding asen = new ASCIIEncoding();
        //Debug.Log("Transmitting..." + sendmsg);
        byte[] ba = asen.GetBytes(sendmsg);
        if (m_stream != null) m_stream.Write(ba, 0, ba.Length);
        else Debug.Log("bye bye");
    }

    void Func()
    {
        //创建服务器连接
        //connect to server
        Debug.Log("try connect");
        TcpClient cln = new TcpClient();
        cln.Connect("192.168.0.105", 8899);
        m_stream = cln.GetStream();
        Debug.Log("connected");

        //发送一个创建player消息
        Message msg = new Message();
        msg.id = m_LocalNumber;
        msg.type = 0;
        Send(msg);

        // Buffer to store the response bytes.
        Byte[] data = new Byte[256];

        while (threadRun)
        {
            String responseData = String.Empty;

            Debug.Log("wait for msg");
            // Read the first batch of the TcpServer response bytes.
            Int32 bytes = m_stream.Read(data, 0, data.Length);
            responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);

            Debug.Log("msg received :" + responseData);

            string[] sArray = responseData.Split('#');
            if (sArray.Length == 0)
            {
                Debug.Log("breakbreakbreak");
                break;
            }
            for (int i = 0; i < sArray.Length; i++)
            {
                Message recv = Message.CreateFromJSON(sArray[i]);
                Debug.Log("already pase this msg" + sArray[i]);
                if (recv == null)
                {
                    //Debug.Log("error parse: messgae id" + Convert.ToString(recv.id) + " message type:" + Convert.ToString(recv.type));
                    continue;
                }
                switch(recv.type)
                {
                    case 0:
                        m_ConstructQueue.Enqueue(recv);
                        break;
                    case 3:
                        m_ConstructWavesQueue.Enqueue(recv);
                        break;
                    case 4:
                        restart = true;
                        break;
                    default:
                        if (m_Players.ContainsKey(recv.id))
                        {
                            m_Players[recv.id].m_MsgQueue.Enqueue(recv);
                            Debug.Log("push move msg");
                        }
                        break;
                }
            }
            Debug.Log("loop over");
        }
        Debug.Log("over");
        // Close everything.
        lock (thisLock)
        {
            m_stream.Close();
            m_stream = null;
        }
        cln.Close();
    }
}