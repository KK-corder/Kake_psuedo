using System.IO;
using System.Text;
using UnityEngine;

public class CSVloader : MonoBehaviour
{
    private StreamWriter sw;

    void Start()
    {
        sw = new StreamWriter(@"SaveData.csv", true, Encoding.GetEncoding("Shift_JIS"));

        string header = "UserID,TrialCount,Session1ProgressRate,Session2ProgressRate,weight";
        sw.WriteLine(header);
        sw.Flush();
    }

    public void SaveData(
        string userID, string trial, string session1Rate, string session2Rate, string weight)
    {
        string line = $"{userID},{trial},{session1Rate},{session2Rate},{weight}";
        sw.WriteLine(line);
        sw.Flush();
    }

    private void OnDestroy()
    {
        if (sw != null)
        {
            sw.Close();
        }
    }
}
