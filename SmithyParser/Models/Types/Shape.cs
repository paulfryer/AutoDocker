﻿namespace SmithyParser.Models.Types;

public abstract class Shape
{
    public string ShapeId { get; set; }

    protected Shape(string shapeId)
    {
        ShapeId = shapeId;
    }

    public string Namespace => ShapeId.Split('#')[0];

    public string Name => ShapeId.Split('#')[1];
}