using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum IngrediantUnit { Spoon, Cup, Bowl, Piece }

[Serializable]
public class Ingredient
{
    public string name;
    public int amount = 1;
    public IngrediantUnit unit;
}

public class Recipe : MonoBehaviour
{
    public Ingredient potionResult;
    public Ingredient[] potionIngredients;
}
