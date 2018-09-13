
namespace DisUtil
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class DisProtobuf : IDisProto
    {
        private const int kMaxVarintBytes = 10;
        private const int kMaxVarint32Bytes = 5;
        private Action<string> _errorLogger;

        private enum WireType
        {
            WIRETYPE_VARINT = 0,
            WIRETYPE_FIXED64 = 1,
            WIRETYPE_LENGTH_DELIMITED = 2,
            WIRETYPE_START_GROUP = 3,
            WIRETYPE_END_GROUP = 4,
            WIRETYPE_FIXED32 = 5,
        };

        public void SetLogger(Action<string> logger)
        {
            _errorLogger = logger;
        }

        private void LogError(object format, params object[] param)
        {
            if (_errorLogger != null)
            {
                _errorLogger(string.Format(format.ToString(), param));
            }
            else
            {
                Console.WriteLine(string.Format(format.ToString(), param));
            }
        }

        private AnyProto _DoParse(byte[] data, uint start, uint len, bool log = true)
        {
            try
            {
                AnyProto anyProto = new AnyProto();

                uint index = start;
                while (len - index > 0)
                {
                    ProtoFiled field = new ProtoFiled();
                    uint value = 0;
                    uint used = 0;

                    if (!ReadVarint32(out value, out used, data, index, len))
                    {
                        if (log) LogError("ReadVarint32 Failed");
                        return null;
                    }

                    index += used;
                    field.Index = value >> 3;
                    uint nWireType = value & 0x7;
                    if (nWireType > (uint)WireType.WIRETYPE_FIXED32)
                    {
                        if (log) LogError("WireType > WIRETYPE_FIXED32");
                        return null;
                    }

                    WireType type = (WireType)nWireType;

                    switch (type)
                    {
                        case WireType.WIRETYPE_VARINT:
                            {
                                ulong vvalue = 0;
                                if (!ReadVarint64(out vvalue, out used, data, index, len))
                                {
                                    if (log) LogError("ReadVarint64 Failed");
                                    return null;
                                }
                                index += used;

                                long lvalue = (long)vvalue;
                                if (lvalue > int.MaxValue)
                                {
                                    field.FieldType = typeof(long);
                                    field.FieldValue = lvalue;
                                }
                                else
                                {
                                    field.FieldType = typeof(int);
                                    field.FieldValue = (int)vvalue;
                                }
                            }
                            break;
                        case WireType.WIRETYPE_FIXED32:
                            {
                                int rvalue = 0;
                                rvalue = (int)data[index];
                                rvalue |= ((int)data[index + 1]) << 8;
                                rvalue |= ((int)data[index + 2]) << 16;
                                rvalue |= ((int)data[index + 3]) << 24;

                                field.FieldType = typeof(int);
                                field.FieldValue = rvalue;

                                index += 4;
                            }
                            break;
                        case WireType.WIRETYPE_FIXED64:
                            {
                                long rvalue = 0;
                                rvalue = (long)data[index];
                                rvalue |= ((long)data[index + 1]) << 8;
                                rvalue |= ((long)data[index + 2]) << 16;
                                rvalue |= ((long)data[index + 3]) << 24;
                                rvalue |= ((long)data[index + 4]) << 32;
                                rvalue |= ((long)data[index + 5]) << 40;
                                rvalue |= ((long)data[index + 6]) << 48;
                                rvalue |= ((long)data[index + 7]) << 56;

                                field.FieldType = typeof(long);
                                field.FieldValue = rvalue;

                                index += 8;
                            }
                            break;
                        case WireType.WIRETYPE_LENGTH_DELIMITED:
                            {
                                if (!ReadVarint32(out value, out used, data, index, len))
                                {
                                    if (log) LogError("ReadVarint32 Failed");
                                    return null;
                                }
                                index += used;

                                if (index + value > len)
                                {
                                    //Core.Debug.LogError("used data beyond data size");
                                    value = (uint)data.Length - index;
                                    return null;
                                }

                                AnyProto tryProto = _DoParse(data, index, index + value, false);
                                if (tryProto != null)
                                {
                                    index += value;

                                    field.FieldType = typeof(object);
                                    field.IsObject = true;
                                    field.FieldValue = tryProto;
                                }
                                else
                                {
                                    if (index + value > data.Length)
                                    {
                                        //Core.Debug.LogError("used data beyond data size");
                                        value = (uint)data.Length - index;
                                        return null;
                                    }
                                    var getValue = Encoding.UTF8.GetString(data, (int)index, (int)value);
                                    index += value;

                                    field.FieldType = typeof(string);
                                    field.FieldValue = getValue;
                                }
                            }
                            break;
                        case WireType.WIRETYPE_START_GROUP:
                            break;
                        case WireType.WIRETYPE_END_GROUP:
                            break;
                        default:
                            if (log) LogError("unexcept");
                            break;
                    }

                    bool added = false;
                    foreach (var fld in anyProto.Fields)
                    {
                        if (fld.Index == field.Index)
                        {
                            List<ProtoFiled> reptead = null;
                            if (!fld.Repeated)
                            {
                                fld.Repeated = true;
                                reptead = new List<ProtoFiled>();
                                var nfld = new ProtoFiled();
                                nfld.FieldType = fld.FieldType;
                                nfld.FieldValue = fld.FieldValue;
                                nfld.Index = fld.Index;
                                nfld.Repeated = false;
                                reptead.Add(nfld);
                                fld.FieldValue = reptead;
                            }
                            else
                            {
                                reptead = (List<ProtoFiled>)fld.FieldValue;
                            }

                            reptead.Add(field);
                            added = true;
                            break;
                        }
                    }
                    if (!added)
                    {
                        anyProto.Fields.Add(field);
                    }
                }

                return anyProto;
            }
            catch (System.Exception e)
            {
                //Core.Debug.LogError(e);
                return null;
            }
        }

        public AnyProto Parse(byte[] data, uint len)
        {
            try
            {
                return _DoParse(data, 0, len);
            }
            catch (Exception e)
            {
                //Core.Debug.Log(e);
                return null;
            }
        }

        public static bool ReadVarint32(out uint value, out uint used, byte[] data, uint start, uint len)
        {
            value = 0;
            used = 0;

            if (start > len) return false;
            if (data.Length < len || len == 0) return false;

            if (data[start] < 0x80)
            {
                used = 1;
                value = data[start];
            }
            else
            {
                if (!ReadVarint32Fallback(ref value, ref used, data, start, len)) return false;
            }

            return true;
        }

        public static bool ReadVarint64(out ulong value, out uint used, byte[] data, uint start, uint len)
        {
            value = 0;
            used = 0;

            if (start > len) return false;
            if (data.Length < len || len == 0) return false;

            if (data[start] < 0x80)
            {
                used = 1;
                value = data[start];
            }
            else
            {
                if (!ReadVarint64Fallback(ref value, ref used, data, start, len)) return false;
            }

            return true;
        }

        private static bool ReadVarint32Fallback(ref uint value, ref uint used, byte[] data, uint start, uint len)
        {
            if (len - start >= kMaxVarintBytes || (data[len-1] & 0x80) != 0)
            {
                return ReadVarint32FromArray(ref value, ref used, data, start, len);
            }
            else
            {
                return ReadVarint32Slow(ref value, ref used, data, start, len);
            }
        }

        private static bool ReadVarint32Slow(ref uint value, ref uint used, byte[] data, uint start, uint len)
        {
            ulong result = 0;
            if (!ReadVarint64Fallback(ref result, ref used, data, start, len)) return false;

            value = (uint)result;
            return true;
        }

        private static bool ReadVarint64Slow(ref ulong value, ref uint used, byte[] data, uint start, uint len)
        {
            ulong result = 0;
            int count = 0;
            uint b = 0;

            do
            {
                if (count == kMaxVarintBytes) return false;
                if (count >= len - start) return false;

                b = data[start + count];
                result |= ((ulong)(b & 0x7F)) << (7 * count);
                ++count;
            } while ((b & 0x80) != 0);

            value = result;
            used = (uint)count;
            return true;
        }

        private static bool ReadVarint64Fallback(ref ulong value, ref uint used, byte[] data, uint start, uint len)
        {
            if (len - start >= kMaxVarintBytes || (data[len - 1] & 0x80) != 0)
            {
                return ReadVarint64FromArray(ref value, ref used, data, start, len);
            }
            else
            {
                return ReadVarint64Slow(ref value, ref used, data, start, len);
            }
        }

        private static bool ReadVarint64FromArray(ref ulong value, ref uint used, byte[] data, uint start, uint len)
        {
            uint part0 = 0, part1 = 0, part2 = 0;
            do
            {
                uint b = 0;

                b = data[start + used++]; part0  = (b & 0x7F)      ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part0 |= (b & 0x7F) << 7 ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part0 |= (b & 0x7F) << 14; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part0 |= (b & 0x7F) << 21; if ((b & 0x80) == 0) break;

                b = data[start + used++]; part1  = (b & 0x7F)      ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part1 |= (b & 0x7F) << 7 ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part1 |= (b & 0x7F) << 14; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part1 |= (b & 0x7F) << 21; if ((b & 0x80) == 0) break;

                b = data[start + used++]; part2  = (b & 0x7F)      ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; part2 |= (b & 0x7F) << 7 ; if ((b & 0x80) == 0) break;

                return false;
            } while (false);

            value = ((ulong)part0) | (((ulong)part1) << 28) | (((ulong)part2) << 56);
            return true;
        }

        private static bool ReadVarint32FromArray(ref uint value, ref uint used, byte[] data, uint start, uint len)
        {
            uint result = 0;
            do
            {
                uint b = 0;

                b = data[start + used++]; result  = (b & 0x7F)      ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; result |= (b & 0x7F) << 7 ; if ((b & 0x80) == 0) break;
                b = data[start + used++]; result |= (b & 0x7F) << 14; if ((b & 0x80) == 0) break;
                b = data[start + used++]; result |= (b & 0x7F) << 21; if ((b & 0x80) == 0) break;
                b = data[start + used++]; result |= (b       ) << 28; if ((b & 0x80) == 0) break;

                bool no_error = false;
                for (int i = kMaxVarint32Bytes; i < kMaxVarintBytes; ++i,++used)
                {
                    b = data[start + i];
                    if ((b & 0x80) != 0)
                    {
                        no_error = true;
                        break;
                    }
                }

                if (!no_error) return false;

            } while (false);

            value = result;
            return true;
        }
    }

    public interface IProtocolPrint
    {
        string Print(AnyProto proto);
    }

    public class ProtocolPrint : IProtocolPrint
    {
        public string Print(AnyProto proto)
        {
            return _DoPrint(proto, 0);
        }

        private string _DoPrintField(ProtoFiled field, int tab)
        {
            if (field.FieldType == typeof(object))
            {
                return "\n" + _DoPrint((AnyProto)field.FieldValue, tab);
            }
            else
            {
                return " " + field.FieldValue.ToString();
            }
        }

        private string _DoPrint(AnyProto proto, int tab)
        {
            StringBuilder sb = new StringBuilder();
            string strtab = "";
            for (var i = 0; i < tab; ++i)
            {
                strtab += "\t";
            }
            foreach (var fld in proto.Fields)
            {
                if (fld.Repeated)
                {
                    var repeated = (List<ProtoFiled>)fld.FieldValue;
                    sb.Append(string.Format("{2}{0} repeated {1}", fld.Index, GetTypeName(fld.FieldType), strtab));
                    foreach (var refld in repeated)
                    {
                        sb.Append(string.Format(" {0}", _DoPrintField(refld, tab + 1)));
                    }
                    sb.Append("\n");
                }
                else
                {
                    sb.Append(string.Format("{3}{0} {1} {2}\n", fld.Index, GetTypeName(fld.FieldType), _DoPrintField(fld, tab+1), strtab));
                }
            }

            return sb.ToString();
        }

        private string GetTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(object)) return "message";

            return "unknow";
        }
    }
}
