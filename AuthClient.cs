using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class AuthData
{
    public string id;
    public string secret;
}

[Serializable]
class RegisterBody
{
    public string name;
}

// Rotas esperadas no servidor (usando register() e login() do auth.ts):
//   POST /register  corpo {"name": "..."}            -> 200 {"id": "...", "secret": "..."}
//   POST /login     corpo {"id": "...", "secret": ""} -> 200 se ok, 401 se invalido
public class AuthClient : MonoBehaviour
{
    // persistentDataPath funciona no PC e no celular
    string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, "auth.json"); }
    }

    // Chame com StartCoroutine(auth.Connect(url, nome, ok => ...));
    // Se ok for true, pode ir direto para o menu inicial.
    public IEnumerator Connect(string baseUrl, string name, Action<bool> onDone)
    {
        AuthData auth = Load();

        if (auth == null)
        {
            // Primeira vez: cria a conta e guarda id + segredo no aparelho
            string body = JsonUtility.ToJson(new RegisterBody { name = name });
            bool registered = false;

            yield return Post(baseUrl + "/register", body, (ok, json) =>
            {
                if (!ok) return;
                auth = JsonUtility.FromJson<AuthData>(json);
                Save(auth);
                registered = true;
            });

            onDone(registered);
            yield break;
        }

        // Proximas vezes: manda id + segredo e entra direto
        bool logged = false;
        yield return Post(baseUrl + "/login", JsonUtility.ToJson(auth), (ok, json) => logged = ok);

        // Se falhar, NAO apague o arquivo automaticamente:
        // isso criaria uma conta nova sem o jogador perceber.
        onDone(logged);
    }

    IEnumerator Post(string url, string json, Action<bool, string> callback)
    {
        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            bool ok = req.responseCode == 200 && string.IsNullOrEmpty(req.error);
            callback(ok, req.downloadHandler.text);
        }
    }

    AuthData Load()
    {
        try
        {
            if (!File.Exists(SavePath)) return null;
            return JsonUtility.FromJson<AuthData>(File.ReadAllText(SavePath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    void Save(AuthData data)
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }
}
