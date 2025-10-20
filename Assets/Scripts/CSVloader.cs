using System.IO;
using System.Text;
using UnityEngine;

public class CSVloader : MonoBehaviour
{
    private StreamWriter sw;

    void Start()
    {
        sw = new StreamWriter(@"SaveData.csv", true, Encoding.GetEncoding("Shift_JIS"));

        string header = "UserID,TrialCount,ProgressRate, Distance, IntervalTime, LimitDistance, selectanswer, TouchCount, weight";
        sw.WriteLine(header);
        sw.Flush();
    }

    public void SaveData(
        string userID, string trial, string rate,
        string distance, string interval,
        string limitDistance, string selectAnswer, string touchCount, string weight)
    {
        string line = $"{userID},{trial},{rate},{distance}," +
                      $"{interval},{limitDistance},{selectAnswer},{touchCount},{weight}";
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
