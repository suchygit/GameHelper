
using System;
using System.Collections.Generic;

public class ProtoFiled
{
    public ulong Index = 0;
    public Type FieldType;
    public object FieldValue = null;
    public bool Repeated = false;
    public bool IsObject = false;
}

public class AnyProto
{
    public List<ProtoFiled> Fields = new List<ProtoFiled>();

    public ProtoFiled GetField(int index)
    {
        foreach (var f in Fields)
        {
            if ((int)f.Index == index) return f;
        }

        return null;
    }
}

public interface IDisProto
{
    AnyProto Parse(byte[] data, uint len);
    void SetLogger(Action<string> logger);
}