using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public enum MessageType : byte
{
    Connect       = 1,
    Welcome       = 2,
    Spawn         = 3,
    Despawn       = 4,
    Move          = 5,
    Snapshot      = 6,
    PickupRequest = 7,
    PickupAck     = 8,
    BonusState    = 9,
    ObjectState   = 10,
    BonusSpawn    = 11,
    GameOver      = 12,
    TimerSync     = 13,
}

public struct PlayerState
{
    public int Id;
    public byte Character;
    public string PlayerName;
    public int Score;
    public Vector3 Position;
    public float Yaw;
}

public static class PlayerNames
{
    public const string DefaultName = "Player";
    public const int MaxLength = 16;

    public static string Normalize(string playerName)
    {
        string normalized = string.IsNullOrWhiteSpace(playerName) ? DefaultName : playerName.Trim();
        return normalized.Length <= MaxLength ? normalized : normalized.Substring(0, MaxLength);
    }
}

public class PacketWriter
{
    public const int HeaderSize = 5;

    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;
    private readonly MessageType _type;
    private readonly ushort _seq;

    public PacketWriter(MessageType type, ushort seq = 0)
    {
        _type = type;
        _seq = seq;
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
    }

    public void WriteByte(byte value)   => _writer.Write(value);
    public void WriteInt(int value)     => _writer.Write(value);
    public void WriteFloat(float value) => _writer.Write(value);

    public void WriteVector3(Vector3 v)
    {
        _writer.Write(v.x);
        _writer.Write(v.y);
        _writer.Write(v.z);
    }

    public void WriteQuaternion(Quaternion q)
    {
        _writer.Write(q.x);
        _writer.Write(q.y);
        _writer.Write(q.z);
        _writer.Write(q.w);
    }

    public void WriteString(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
        _writer.Write((ushort)bytes.Length);
        _writer.Write(bytes);
    }

    public void WritePlayerState(PlayerState p)
    {
        WriteInt(p.Id);
        WriteByte(p.Character);
        WriteString(PlayerNames.Normalize(p.PlayerName));
        WriteInt(p.Score);
        WriteVector3(p.Position);
        WriteFloat(p.Yaw);
    }

    public void WritePositionQuantized(Vector3 v)
    {
        _writer.Write(Quantize(v.x));
        _writer.Write(Quantize(v.y));
        _writer.Write(Quantize(v.z));
    }

    public void WriteYawQuantized(float yaw)
    {
        _writer.Write((byte)(Mathf.Repeat(yaw, 360f) / 360f * 256f));
    }

    private static short Quantize(float v)
    {
        return (short)Mathf.Clamp(Mathf.Round(v * 100f), short.MinValue, short.MaxValue);
    }

    public byte[] ToBytes()
    {
        byte[] payload = _stream.ToArray();
        byte[] packet = new byte[HeaderSize + payload.Length];

        packet[0] = (byte)_type;
        packet[1] = (byte)(_seq & 0xFF);
        packet[2] = (byte)((_seq >> 8) & 0xFF);
        ushort len = (ushort)payload.Length;
        packet[3] = (byte)(len & 0xFF);
        packet[4] = (byte)((len >> 8) & 0xFF);

        System.Array.Copy(payload, 0, packet, HeaderSize, payload.Length);
        return packet;
    }
}

public class PacketReader
{
    public MessageType Type { get; private set; }
    public ushort Seq { get; private set; }
    public ushort PayloadLength { get; private set; }

    private readonly BinaryReader _reader;

    public PacketReader(byte[] data)
    {
        var stream = new MemoryStream(data);
        _reader = new BinaryReader(stream);

        Type = (MessageType)_reader.ReadByte();
        Seq = _reader.ReadUInt16();
        PayloadLength = _reader.ReadUInt16();
    }

    public byte ReadByte()   => _reader.ReadByte();
    public int ReadInt()     => _reader.ReadInt32();
    public float ReadFloat() => _reader.ReadSingle();

    public Vector3 ReadVector3()
    {
        float x = _reader.ReadSingle();
        float y = _reader.ReadSingle();
        float z = _reader.ReadSingle();
        return new Vector3(x, y, z);
    }

    public Quaternion ReadQuaternion()
    {
        float x = _reader.ReadSingle();
        float y = _reader.ReadSingle();
        float z = _reader.ReadSingle();
        float w = _reader.ReadSingle();
        return new Quaternion(x, y, z, w);
    }

    public string ReadString()
    {
        ushort length = _reader.ReadUInt16();
        byte[] bytes = _reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    public PlayerState ReadPlayerState()
    {
        return new PlayerState
        {
            Id = ReadInt(),
            Character = ReadByte(),
            PlayerName = PlayerNames.Normalize(ReadString()),
            Score = ReadInt(),
            Position = ReadVector3(),
            Yaw = ReadFloat(),
        };
    }

    public Vector3 ReadPositionQuantized()
    {
        float x = _reader.ReadInt16() / 100f;
        float y = _reader.ReadInt16() / 100f;
        float z = _reader.ReadInt16() / 100f;
        return new Vector3(x, y, z);
    }

    public float ReadYawQuantized()
    {
        return _reader.ReadByte() / 256f * 360f;
    }
}

public class PacketFramer
{
    private readonly List<byte> _buffer = new List<byte>();

    public void Push(byte[] data, int count)
    {
        for (int i = 0; i < count; i++) _buffer.Add(data[i]);
    }

    public void Clear()
    {
        _buffer.Clear();
    }

    public bool TryRead(out byte[] packet)
    {
        packet = null;
        if (_buffer.Count < PacketWriter.HeaderSize) return false;

        int len = _buffer[3] | (_buffer[4] << 8);
        int total = PacketWriter.HeaderSize + len;
        if (_buffer.Count < total) return false;

        packet = _buffer.GetRange(0, total).ToArray();
        _buffer.RemoveRange(0, total);
        return true;
    }
}
