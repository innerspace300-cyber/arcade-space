using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int health = 16;
    public int mana = 12;
    public int stamina = 15;

    private int maxSP;
    private int maxHP;
    private int maxMP;

    public int MaxHP { get => maxHP; 
            set
            {
                maxHP = value;
                health = value;
            }
        }
    public int MaxMP { get => maxMP; 
        set
        {
            maxMP = value;
            mana = value;
        }
    }
    public int MaxSP { get => maxSP; 
        set
        {
            maxSP = value;
            stamina = value;
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        MaxHP = health;
        MaxMP = mana;
        MaxSP = stamina;
    }

    private void Update()
    {
        UpdateStats();  
    }

    private void UpdateStats()
    {
        if (health > MaxHP)
        {
            MaxHP = health;
        }

        if (mana > MaxMP)
        {
            MaxMP = mana;
        }

        if (stamina > MaxSP)
        {
            MaxSP = stamina;
        }
    }
}
