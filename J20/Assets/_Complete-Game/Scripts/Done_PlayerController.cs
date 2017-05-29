using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
[System.Serializable]
public class Done_Boundary 
{
	public float xMin, xMax, zMin, zMax;
}

public class Message
{
    public int id; //消息作用玩家id
    public float h = 0.0f;
    public float v = 0.0f;
    public int type;  //0 创建player 1 move 2 shoot

    public string SaveToString()
    {
        return JsonUtility.ToJson(this);
    }

    public static Message CreateFromJSON(string jsonString)
    {
        Message msg = new Message();
        try
        {
            msg = JsonUtility.FromJson<Message>(jsonString);
        }
        catch (ArgumentException)
        {
            Debug.Log("error format");
            return null;
        }
        return msg;
    }
}

public class Done_PlayerController : MonoBehaviour
{
	public float speed;
	public float tilt;
	public Done_Boundary boundary;

	public GameObject shot;
	public Transform shotSpawn;
	public float fireRate;
    public int playerid;
    public int localplayerid;
    [HideInInspector]
    public Done_GameController m_sender;
    [HideInInspector]
    public Queue<Message> m_MsgQueue;

    private float nextFire = 0.0f;

    public bool CheckAndGetMsg(ref Message msg)
    {
        //检查队列是否有消息，有消息则返回消息
        if (m_MsgQueue.Count == 0)
        {
            return false;
        }
        //Debug.Log("msg checked!");
        msg = m_MsgQueue.Dequeue();
        return true;
    }

    void Update ()
	{
        if (playerid == localplayerid && Input.GetButton("Fire1") && Time.time > nextFire)
        {
            nextFire = Time.time + fireRate;
            Message shootcmd = new Message();
            shootcmd.id = localplayerid;
            shootcmd.type = 2;
            m_sender.Send(shootcmd);
        }

        Message cmd = new Message();
        //从队列中获取命令，执行
        while (CheckAndGetMsg(ref cmd))
        {
            switch (cmd.type)
            {
                case 1:
                    Move(cmd.h, cmd.v);
                    break;
                case 2:
                    Shoot();
                    break;
            }
        }
    }

    void FixedUpdate ()
	{
        //  check local player
        if (playerid != localplayerid)
            return;
		float moveHorizontal = Input.GetAxis ("Horizontal");
		float moveVertical = Input.GetAxis ("Vertical");

        //send cmd to server
        //将 x z 与playerid写入发送队列
        Message cmd = new Message();
        cmd.id = localplayerid;
        cmd.h = moveHorizontal;
        cmd.v = moveVertical;
        cmd.type = 1;
        m_sender.Send(cmd);
    }

    void Move(float moveHorizontal, float moveVertical)
    {
        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        GetComponent<Rigidbody>().velocity = movement * speed;

        GetComponent<Rigidbody>().position = new Vector3
        (
            Mathf.Clamp(GetComponent<Rigidbody>().position.x, boundary.xMin, boundary.xMax),
            0.0f,
            Mathf.Clamp(GetComponent<Rigidbody>().position.z, boundary.zMin, boundary.zMax)
        );

        GetComponent<Rigidbody>().rotation = Quaternion.Euler(0.0f, 0.0f, GetComponent<Rigidbody>().velocity.x * -tilt);
    }

    void Shoot()
    {
        Instantiate(shot, shotSpawn.position, shotSpawn.rotation);
        GetComponent<AudioSource>().Play ();
    }
}


