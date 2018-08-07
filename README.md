# ModConfig Helper Library (for TT-QMM Mods)
A library packed up as a mod for other mods to manage their `config.json` file

<hr>

## Installing

### Downloading Latest Build 

You can download the most recently built files [here](https://github.com/Aceba1/TTQMM-ModConfigHelper/tree/master/AcModHelper/bin)
Download the folder and put it in the QMods directory in your game installation.

<hr>

## Using in your mods
To start using this with your project, all you have to do is reference this in your Class Library (References, Add Reference, Browse), and then add the namespace `ModHelper.Config`:
```csharp
using ModHelper.Config;
```
In your patcher method, you can create a new instance of `ModConfig` to handle your `config.json` file.
Using `new ModConfig()` loads your `config.json` in the calling assembly's directory (your mod's folder). If it doesn't exist, an empty file is created for later.
```csharp
public class Patch
{
  public static ModConfig Config; // It'd be helpful to save the ModConfig class if you'd like to use it later

  public static void Main()
  {
    // Harmony things go here...

    Config = new ModConfig();
        
```
There are different ways to use this helper library within your mod. You can use it with a ref list enabled for simplicity and ease, or without to have a different way of controlling your Config. Or, you can have a combination of both.

### With Ref
Now to use this to it's full potential, it would be right to actually use it.
When deciding to use the RefList, the ModConfig class creates a list of Fields you give it to get and set values. All you have to do is give it your desired fields after it loads the config, and it can apply the values it finds connects to each field:
```csharp
public class Patch
{
  public static ModConfig Config;
  
  public static float Setting1; 
  private static int Setting2; // The default value of the field, if the Config does not have it;
  
  public static void Main()
  {
    // Harmony things go here...

    Config = new ModConfig();

    Config.BindConfig<Patch>(null, "Setting1"); // Using BindConfig will set UseRef as true, changing some methods to work with the RefList.
    Config.BindConfig<Patch>(null, "Setting2"); // If 'Setting2' was loaded from the config.json, it will apply the variable to the field.
```
These fields are saved for later, in case you'd like to access them through different methods, or save / load the Config.
If you are using a field from an instance of a class, you must fill out the instance of that class:
```csharp
Config.BindConfig<Class1>(class1a, "sampleText");
Config.BindConfig<Class1>(class1b, "sampleText"); // This will be stored as 'sampleText/1'
```

If you would like to use a field that goes deeper, you can use Reflection to do that:
```csharp
var t = typeof(Program).GetField("Shop").FieldType.GetField("ClosingTime");
Config.BindConfig(Shop, t); // Use field of class or struct Shop in Program
```

`BindConfig` should always be used at the start, and shouldn't be branched off. Try to keep them together.

When you decide that you want to save your binded fields' values, all you have to do is call `WriteConfigJsonFile()` and your variables will be stored in the `config.json`, ready to be put back in their spots the next time it's loaded.

### Without Ref
The sharper way to get around the Config.
After the Config is loaded, you can try to get the values within it for use, and fill in missing ones.
There are different ways to do this, but here is a simple one:
```csharp
Config = new ModConfig();

if (Config["stringVar"] == null) // Indexing ModConfig with an invalid key will return null
{
    Config["stringVar"] = "Tacobell"; // This will create a new Config element
}
```
Now, let's say that you'd like to get a special value from the Config. Such as a class, struct, or field within either one. You can use `GetConfigDeep` to get what you are looking for:
```csharp
Config.GetConfigDeep<float>("struct1", "Position", "z");
```
That there lets you grab the value of a field at the end of a branch you specify. But that's not all:
```csharp
Config.SetConfigDeep(0.125f,"struct1", "Position", "z");
```
There is a matching `SetConfigDeep` that lets you modify a field at the end of a branch, and updates the values going up. 

Remember that if you aren't using the RefList, you will have to manually apply config changes before saving.

If you use the RefList with this code, a few things will change: For example, instead of getting and setting the values found in the Config, it will try to use the binded fields for setting and getting.
Think of it as if the Config was always updated with the latest value of the reference.

Oh, and there's a method that lets you reload your `config.json` file, `ReadConfigJsonFile()`. If you have RefList on, this will overwrite your current values in your references with what was loaded from the file. If you do not, it will simply update the list, and that's it.
