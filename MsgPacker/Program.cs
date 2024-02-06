using MessagePack;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MsgPacker
{
    internal class Program
    {
        struct JPrimitive
        {
            public JTokenType TokenType;
            public object Value;

            public JPrimitive(JTokenType type, object value)
            {
                TokenType = type;
                Value = value;
            }
        }

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (!File.Exists(args[0]))
                {
                    Console.WriteLine("File does not exist!");
                    return;
                }

                string outpath;
                if (args[0].EndsWith(".msg"))
                {
                    Console.WriteLine("Converting MessagePack to JSON");

                    if (args.Length > 1)
                        outpath = args[1];
                    else
                        outpath = Path.GetDirectoryName(args[0]) + "\\" + Path.GetFileNameWithoutExtension(args[0]) + ".json";

                    MessagePackReader reader = new MessagePackReader(File.ReadAllBytes(args[0]));

                    using (FileStream stream = new FileStream(outpath, FileMode.Create, FileAccess.Write))
                    using (StreamWriter streamWriter = new StreamWriter(stream, Encoding.GetEncoding("Shift-JIS")))
                    {
                        JsonTextWriter writer = new JsonTextWriter(streamWriter);
                        writer.Formatting = Formatting.Indented;
                        MsgPackToJson(ref reader, ref writer);

                        writer.Flush();
                        writer.Close();
                    }

                    Console.WriteLine($"Done! Written to \"{outpath}\"");
                }
                else if (args[0].EndsWith(".json"))
                {
                    Console.WriteLine("Converting JSON to MessagePack");

                    if (args.Length > 1)
                        outpath = args[1];
                    else
                        outpath = Path.GetDirectoryName(args[0]) + "\\" + Path.GetFileNameWithoutExtension(args[0]) + ".msg";

                    if (File.Exists(outpath))
                        File.Delete(outpath);

                    using (FileStream inStream = new FileStream(args[0], FileMode.Open, FileAccess.Read))
                        using (StreamReader inReader = new StreamReader(inStream, Encoding.GetEncoding("Shift-JIS")))
                    using (FileStream stream = new FileStream(outpath, FileMode.Create, FileAccess.Write))
                        using (BinaryWriter bWriter = new BinaryWriter(stream))
                    {
                        JsonTextReader reader = new JsonTextReader(inReader);
                        reader.Read();

                        BufferWriter buffer = new BufferWriter(bWriter);
                        MessagePackWriter writer = new MessagePackWriter(buffer);

                        Console.WriteLine("Reading Json to primitives");
                        List<JPrimitive> primitives = JsonToJPrimitives(ref reader);
                        JPrimitivesToMsgPack(primitives.ToArray(), ref writer);

                        writer.Flush();
                        reader.Close();
                    }

                    Console.WriteLine($"Done! Written to \"{outpath}\"");
                }
            }
        }

        static void MsgPackToJson(ref MessagePackReader reader, ref JsonTextWriter writer)
        {
            byte code = reader.NextCode;
            MessagePackType type = reader.NextMessagePackType;

            switch (type)
            {
                case MessagePackType.Integer:
                    switch (code)
                    {
                        default:
                            if ((code & 0xe0) != 0xe0)
                                writer.WriteValue(reader.ReadByte());
                            else
                                writer.WriteValue(reader.ReadSByte());
                            break;
                        case 0xCD:
                            writer.WriteValue(reader.ReadUInt16());
                            break;
                        case 0xCE:
                            writer.WriteValue(reader.ReadUInt32());
                            break;
                        case 0xCF:
                            writer.WriteValue(reader.ReadUInt64());
                            break;
                        case 0xD0:
                            writer.WriteValue(reader.ReadSByte());
                            break;
                        case 0xD1:
                            writer.WriteValue(reader.ReadInt16());
                            break;
                        case 0xD2:
                            writer.WriteValue(reader.ReadInt32());
                            break;
                        case 0xD3:
                            writer.WriteValue(reader.ReadInt64());
                            break;
                    }
                    break;
                case MessagePackType.Nil:
                    reader.ReadNil();
                    writer.WriteUndefined();
                    break;
                case MessagePackType.Boolean:
                    writer.WriteValue(reader.ReadBoolean());
                    break;
                case MessagePackType.Float:
                    writer.WriteValue(reader.ReadSingle());
                    break;
                case MessagePackType.String:
                    writer.WriteValue(reader.ReadString());
                    break;
                case MessagePackType.Binary:
                    writer.WriteValue(reader.ReadBytes());
                    break;
                case MessagePackType.Array:
                    {
                        int count = reader.ReadArrayHeader();
                        writer.WriteStartArray();

                        for (int i = 0; i < count; i++)
                            MsgPackToJson(ref reader, ref writer);

                        writer.WriteEndArray();
                        break;
                    }
                case MessagePackType.Map:
                    {
                        int count = reader.ReadMapHeader();
                        writer.WriteStartObject();

                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadString();
                            writer.WritePropertyName(name);
                            MsgPackToJson(ref reader, ref writer);
                        }

                        writer.WriteEndObject();
                        break;
                    }
            }
        }

        static List<JPrimitive> JsonToJPrimitives(ref JsonTextReader reader)
        {
            List<JPrimitive> primitives = new List<JPrimitive>();

            if (reader.TokenType == JsonToken.None)
                return primitives;

            switch (reader.TokenType)
            {
                default:
                    break;
                case JsonToken.Null:
                    primitives.Add(new JPrimitive(JTokenType.Null, null));
                    break;
                case JsonToken.Integer:
                    primitives.Add(new JPrimitive(JTokenType.Integer, reader.Value));
                    break;
                case JsonToken.Float:
                    primitives.Add(new JPrimitive(JTokenType.Float, (float)reader.Value));
                    break;
                case JsonToken.Boolean:
                    primitives.Add(new JPrimitive(JTokenType.Boolean, reader.Value));
                    break;
                case JsonToken.String:
                    primitives.Add(new JPrimitive(JTokenType.String, reader.Value));
                    break;
                case JsonToken.StartObject:
                    {
                        List<JPrimitive> tokens = new List<JPrimitive>();
                        int count = 0;
                        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                        {
                            tokens.AddRange(JsonToJPrimitives(ref reader));
                            count++;
                        }

                        primitives.Add(new JPrimitive(JTokenType.Object, count));
                        primitives.AddRange(tokens);
                        break;
                    }
                case JsonToken.StartArray:
                    {
                        List<JPrimitive> tokens = new List<JPrimitive>();
                        int count = 0;
                        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                        {
                            tokens.AddRange(JsonToJPrimitives(ref reader));
                            count++;
                        }

                        primitives.Add(new JPrimitive(JTokenType.Array, count));
                        primitives.AddRange(tokens);
                        break;
                    }
                case JsonToken.PropertyName:
                    primitives.Add(new JPrimitive(JTokenType.Property, reader.Value));
                    reader.Read();
                    primitives.AddRange(JsonToJPrimitives(ref reader));
                    break;
            }

            return primitives;
        }

        static void JPrimitivesToMsgPack(JPrimitive[] primitives, ref MessagePackWriter writer)
        {
            for (int i = 0; i < primitives.Length; i++)
            {
                JPrimitive primitive = primitives[i];
                switch (primitive.TokenType)
                {
                    case JTokenType.Undefined:
                    case JTokenType.Null:
                        writer.WriteNil();
                        break;
                    case JTokenType.Integer:
                        writer.Write((int)primitive.Value);
                        break;
                    case JTokenType.Float:
                        writer.Write((float)primitive.Value);
                        break;
                    case JTokenType.Boolean:
                        writer.Write((bool)primitive.Value);
                        break;
                    case JTokenType.Property:
                    case JTokenType.String:
                        byte[] bytes = Encoding.UTF8.GetBytes((string)primitive.Value);
                        writer.WriteString(bytes);
                        break;
                    case JTokenType.Object:
                        writer.WriteMapHeader((int)primitive.Value);
                        break;
                    case JTokenType.Array:
                        writer.WriteArrayHeader((int)primitive.Value);
                        break;
                }
            }
        }
    }

    public class BufferWriter : IBufferWriter<byte>
    {
        byte[] _buffer;
        BinaryWriter _writer;

        public BufferWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        public void Advance(int count)
        {
            _writer.Write(_buffer, 0, count);
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            _buffer = new byte[sizeHint <= 0 ? 0x1000 : sizeHint];

            return _buffer.AsMemory();
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            _buffer = new byte[sizeHint <= 0 ? 0x1000 : sizeHint];

            return _buffer.AsSpan();
        }
    }
}
