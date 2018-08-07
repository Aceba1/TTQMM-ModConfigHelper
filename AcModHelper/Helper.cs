using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ModHelper
{
    namespace Config
    {
        /// <summary>
        /// A helper class to manage your mod's config.json file and use it's values
        /// </summary>
        public class ModConfig
        {
            /// <summary>
            /// Change methods to read and write to binded class fields, making Config management simpler
            /// </summary>
            public bool UseRefList = false;
            
            /// <summary>
            /// Get or set a Config value. If it doesn't exist, make a new one. If trying to get a struct or class, use GetConfigDeep (If Ref: Getting will try to get cooresponding reference. Setting will also set corresponding reference)
            /// </summary>
            /// <param name="key">The name of the variable to index</param>
            /// <returns></returns>
            public object this[string key]
            {
                get
                {
                    object result;
                    if (UseRefList)
                    {
                        if (FieldRefList.TryGetValue(key, out var e))
                        {
                            result = ((FieldInfo)e[0]).GetValue(e[1]);
                        }
                        else
                        {
                            result = config[key];
                        }
                    }
                    else
                    {
                        if (config.ContainsKey(key))
                            result = config[key];
                        else
                            result = null;
                    }
                    return result;
                }
                set
                {
                    config[key] = value;
                    if (UseRefList)
                    {
                        if (FieldRefList.TryGetValue(key, out var e))
                        {
                            ConfigToFieldRef(e[1], (FieldInfo)e[0], key);
                        }
                    }
                }
            }

            private Dictionary<string, object> config = new Dictionary<string, object>();

            /// <summary>
            /// Key:ID Value[0]:FieldInfo Value[1]:Instance
            /// </summary>
            private Dictionary<string, object[]> FieldRefList = new Dictionary<string, object[]>();

            private Dictionary<string, int> FieldRefRepeatCount = new Dictionary<string, int>();

            /// <summary>
            /// The location of the Config file
            /// </summary>
            public string ConfigLocation;

            /// <summary>
            /// Load the Config from the current mod's directory
            /// </summary>
            public ModConfig()
            {
                string path = Path.Combine(Assembly.GetCallingAssembly().Location, "..\\config.json");
                ConfigLocation = path;
                ReadConfigJsonFile(this);
            }

            /// <summary>
            /// Load the Config file from it's path
            /// </summary>
            /// <param name="path">The path of the Config file</param>
            public ModConfig(string path)
            {
                ConfigLocation = path;
                ReadConfigJsonFile(this);
            }

            /// <summary>
            /// Get the FieldInfo of a class's variable
            /// </summary>
            /// <typeparam name="T">The holding class to get the variable from</typeparam>
            /// <param name="VariableName">The name of the variable</param>
            /// <returns>FieldInfo representing the class's variable</returns>
            public static FieldInfo GetFieldInfo<T>(string VariableName) => typeof(T).GetField(VariableName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

            /// <summary>
            /// Get the FieldInfo of a class's variable
            /// </summary>
            /// <param name="T">The holding class to get the variable from</param>
            /// <param name="VariableName">The name of the variable</param>
            /// <returns>FieldInfo representing the class's variable</returns>
            public static FieldInfo GetFieldInfo(Type T, string VariableName) => T.GetField(VariableName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

            /// <summary>
            /// (Ref) Bind a field to the Config for loading and saving, to make life easier.
            /// (Using this will set UseRefList to true)
            /// </summary>
            /// <param name="instance">The class instance to use, null if static</param>
            /// <param name="field">The variable in its class to use, acquire with 'typeof(Class).GetField("variableName")', or ModConfig.GetFieldInfo&lt;Class&gt;("variableName")</param>
            /// <param name="UpdateRef">Set the value of the variable to what's in the Config, if it exists</param>
            public void BindConfig(object instance, FieldInfo field, bool UpdateRef = true)
            {
                if (!UseRefList)
                    UseRefList = true;

                int cache = 0;
                string ats = "";
                if (FieldRefRepeatCount.TryGetValue(field.Name, out cache))
                {
                    ats = "/" + cache.ToString();
                }

                FieldRefRepeatCount[field.Name] = cache + 1;

                FieldRefList.Add(field.Name + ats, new object[] { field, instance });

                if (UpdateRef)
                    ConfigToFieldRef(instance, field, field.Name + ats);
            }

            /// <summary>
            /// (Ref) Bind a field to the Config for loading and saving, to make life easier.
            /// (Using this will set UseRefList to true)
            /// </summary>
            /// <typeparam name="T">The class (holding the variable)'s type</typeparam>
            /// <param name="instance">The class instance to use, null if static</param>
            /// <param name="VariableName">The name of the variable</param>
            /// <param name="UpdateRef">Set the value of the variable to what's in the Config, if it exists</param>
            public void BindConfig<T>(T instance, string VariableName, bool UpdateRef = true)
            {
                BindConfig(instance, GetFieldInfo<T>(VariableName), UpdateRef);
            }

            private void ConfigToFieldRef(object instance, FieldInfo field, string Search)
            {
                try
                {
                    if (field.FieldType == typeof(float))
                    {
                        float cache = 0f;
                        if (TryGetConfigF(Search, ref cache))
                        {
                            field.SetValue(instance, cache);
                        }
                    }
                    else
                    {
                        object cache = null;
                        if (TryGetConfig(Search, ref cache))
                        {
                            try
                            {
                                field.SetValue(instance, cache);
                            }
                            catch
                            {
                                try
                                {
                                    field.SetValue(instance, Convert.ChangeType(cache, field.FieldType));
                                }
                                catch
                                {
                                    field.SetValue(instance, ((Newtonsoft.Json.Linq.JObject)cache).ToObject(field.FieldType));
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Something wrong happened while trying to set a FieldInfo value\n" + e.Message + "\nThe FieldInfo was " + (field.FieldType == typeof(float) ? "a float (" : "not a float (") + field.FieldType.ToString() + ")\nThe name being searched for: " + Search);
                }
            }

            /// <summary>
            /// Try to get a value of a specified name from the RAW Config
            /// </summary>
            /// <typeparam name="T">The type of object being acquired</typeparam>
            /// <param name="ConfigID">The name of the object to try to get</param>
            /// <param name="value">Returns the object as type if it exists</param>
            /// <returns>Returns true if the object exists</returns>
            public bool TryGetConfig<T>(string ConfigID, ref T value)
            {
                object cache = null;
                bool result = this.config.TryGetValue(ConfigID, out cache);
                if (result)
                {
                    value = (T)cache;
                }
                return result;
            }

            /// <summary>
            /// Try to get a float value of a specified name from the RAW Config
            /// </summary>
            /// <param name="ConfigID">The name of the float value to try to get</param>
            /// <param name="value">Returns the float value if it exists</param>
            /// <returns>Returns true if the object exists</returns>
            public bool TryGetConfigF(string ConfigID, ref float value)
            {
                object cache = null;
                bool result = this.config.TryGetValue(ConfigID, out cache);
                if (result)
                {
                    value = Convert.ToSingle(cache);
                }
                return result;
            }

            /// <summary>
            /// Apply a value to a variable or branched off variable in the Config. (such as a struct or class field)
            /// This should be the preferred method. (If Ref: Try to set the value in the reference)
            /// </summary>
            /// <param name="Value">The value to set at the end of the search</param>
            /// <param name="keys">The keys to search with to get the variable</param>
            public void SetConfigDeep(object Value, params string[] keys)
            {
                if (UseRefList)
                {
                    if (FieldRefList.TryGetValue(keys[0], out var e))
                    {
                        object lookinst = null;
                        {
                            FieldInfo result = (FieldInfo)e[0];
                            lookinst = GetFieldInfo(result.DeclaringType, keys[0]).GetValue(e[1]);
                            object[] instlist = new object[keys.Length];
                            instlist[0] = lookinst;
                            FieldInfo[] resultlist = new FieldInfo[keys.Length];
                            resultlist[0] = result;
                            for (int i = 1; i < keys.Length; i++)
                            {
                                result = GetFieldInfo(result.FieldType, keys[i]);
                                if (i + 1 == keys.Length)
                                {
                                    result.SetValue(lookinst, Value);
                                    for (int j = i - 1; j > 0; j--)
                                        resultlist[j].SetValue(instlist[j - 1], instlist[j]);
                                    return;
                                }
                                lookinst = result.GetValue(lookinst);
                                resultlist[i] = result;
                                instlist[i] = lookinst;
                            }
                        }
                    }
                }
                if (config[keys[0]] is JToken)
                {
                    JToken result;
                    result = (JToken)config[keys[0]];
                    for (int i = 1; i < keys.Length-1; i++)
                    {
                        result = result[keys[i]];
                    }
                    JToken jToken = JToken.FromObject(Value);
                    result[keys[keys.Length-1]] = jToken.ToString();
                    return;
                }
                else
                {
                    Type result = config[keys[0]].GetType();
                    object lookinst = config[keys[0]];
                    for (int i = 1; i < keys.Length; i++)
                    {
                        FieldInfo fieldInfo = GetFieldInfo(result, keys[i]);
                        if (i + 1 == keys.Length)
                        {
                            fieldInfo.SetValue(lookinst, Value);
                            return;
                        }
                        lookinst = fieldInfo.GetValue(lookinst);
                        result = fieldInfo.FieldType;
                    }
                }
            }

            /// <summary>
            /// Get the value of a variable or branched off variable in the Config. (such as a struct or class field)
            /// This should be the preferred method. (If Ref: Try to get the current value in the reference)
            /// </summary>
            /// <typeparam name="T">The object type to return from the end of the search</typeparam>
            /// <param name="keys">The keys to search with to get the variable</param>
            /// <returns>the value found at the end</returns>
            public T GetConfigDeep<T>(params string[] keys)
            {
                if (UseRefList)
                {
                    if (FieldRefList.TryGetValue(keys[0], out var e))
                    {
                        object lookinst = null;
                        {
                            FieldInfo result = (FieldInfo)e[0];
                            lookinst = GetFieldInfo(result.DeclaringType, keys[0]).GetValue(e[1]);
                            for (int i = 1; i < keys.Length; i++)
                            {
                                result = GetFieldInfo(result.FieldType, keys[i]);
                                lookinst = result.GetValue(lookinst);
                            }
                        }
                        return (T)lookinst;
                    }
                }
                if (config[keys[0]] is JToken)
                {
                    JToken result = (JToken)config[keys[0]];
                    for (int i = 1; i < keys.Length; i++)
                    {
                        result = result[keys[i]];
                    }
                    return result.ToObject<T>();
                }
                else
                {
                    Type result = config[keys[0]].GetType();
                    object lookinst = config[keys[0]];
                    for (int i = 1; i < keys.Length; i++)
                    {
                        FieldInfo fieldInfo = GetFieldInfo(result, keys[i]);
                        lookinst = fieldInfo.GetValue(lookinst);
                        result = fieldInfo.FieldType;
                    }
                    return (T)lookinst;
                }
            }

            /// <summary>
            /// Write Config data to the file
            /// (If Ref: Apply all referenced fields to the Config before serializing)
            /// </summary>
            /// <param name="Config">The ModConfig to write the Config of</param>
            /// <returns>Returns true if successful</returns>
            
            public static bool WriteConfigJsonFile(ModConfig Config)
            {
                return Config.WriteConfigJsonFile();
            }

            /// <summary>
            /// Write Config data to the file
            /// (If Ref: Apply all referenced fields to the Config before serializing)
            /// </summary>
            /// <returns>Returns true if successful</returns>
            public bool WriteConfigJsonFile()
            {
                try
                {
                    if (UseRefList)
                        foreach (var field in FieldRefList)
                        {
                            FieldInfo finfo = (FieldInfo)field.Value[0];
                            config[field.Key] = finfo.GetValue(field.Value[1]);
                        }

                    string json = JsonConvert.SerializeObject(config, Formatting.Indented);

                    File.WriteAllText(ConfigLocation, json);

                    return true;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log("ERROR! config.json deserialization failed.\n" + e.Message + "\n" + e.StackTrace);
                    return false;
                }
            }

            /// <summary>
            /// Reload all the Config values and push them to references.
            /// This should not be used normally, or should be needed
            /// </summary>
            public void ReapplyConfigToRef()
            {
                if (UseRefList)
                    foreach (var field in FieldRefList)
                    {
                        ConfigToFieldRef(field.Value[1], (FieldInfo)field.Value[0], field.Key);
                    }
            }

            /// <summary>
            /// Reload the Config file
            /// (If Instance Ref: Apply Config changes to references)
            /// </summary>
            /// <param name="Config">The ModConfig class to add changes to</param>
            /// <returns>Returns true if successful</returns>
            public static bool ReadConfigJsonFile(ModConfig Config)
            {
                return Config.ReadConfigJsonFile();
            }

            /// <summary>
            /// Reload the Config file
            /// </summary>
            /// <returns>Returns true if successful</returns>
            public bool ReadConfigJsonFile()
            {
                try
                {
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };
                    if (System.IO.File.Exists(ConfigLocation))
                    {
                        string json = File.ReadAllText(ConfigLocation);
                        var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, settings);
                        foreach (var pair in config)
                        {
                            try
                            {
                                this[pair.Key] = pair.Value;
                            }
                            catch (Exception e)
                            {
                                UnityEngine.Debug.Log("ERROR!\n" + pair.Key + "\n" + e.Message + "\n" + e.StackTrace);
                            }
                        }
                        return true;
                    }
                    else
                    {
                        File.WriteAllText(ConfigLocation, "{\n}");
                        return false;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log("ERROR! config.json deserialization failed.\n" + e.Message + "\n" + e.StackTrace);
                    return false;
                }
            }

            /// <summary>
            /// This does nothing, nothing at all.
            /// </summary>
            public static void Patch()
            {
            }
        }
    }
}