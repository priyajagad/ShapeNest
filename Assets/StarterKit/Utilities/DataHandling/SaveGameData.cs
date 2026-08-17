using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;


public static class SaveGameData
{

    public static string filePath
    {
        get { return Application.persistentDataPath + "/GameData.dat"; }
    }

    public static void Save<T>(T _data, string password)
    {
        var m_data = JsonUtility.ToJson(_data);
        m_data = DataEncoder.EncryptThis(m_data, password);
        File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes(m_data));
    }

    public static T Load<T>(T defaultData, string password)
    {
        if (!File.Exists(filePath))
        {
            Save(defaultData, password);
            return Load(defaultData, password);
        }

        var m_data = File.ReadAllBytes(filePath);
        var dataAsText = Encoding.UTF8.GetString(m_data);
        dataAsText = DataEncoder.Decrypt(dataAsText, password);

        if (Debug.isDebugBuild)
            Debug.Log(dataAsText);

        return JsonUtility.FromJson<T>(dataAsText);
    }

    public static T Clear<T>(T defaultData, string password)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        Save(defaultData, password);

        return Load(defaultData, password);
    }

    public static void Delete()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public static string GetEncryptedData(string data, string password)
    {
        return DataEncoder.EncryptThis(data, password);
    }

    public static string GetDecryptedData(string data, string password)
    {
        return DataEncoder.Decrypt(data, password);
    }
}