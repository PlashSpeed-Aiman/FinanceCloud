// For more information see https://aka.ms/fsharp-console-apps
open System
open System.Data.SQLite
open Microsoft.FSharp.Core
(*
    Sort by Month, Category, Get Average
    For all expense, print to PDF


*)
open System
open FSharp.Core
open FSharp.Data
open XPlot.GoogleCharts

type FinanceCategories =
    | Food
    | Lifestyle
    | Utilities
    | Others
type FinanceEntry =
    {EntryName:string;Amount:double;EntryDate:DateTime;Category:FinanceCategories}

let getAmount item = item.Amount

let db_connect () =
    let sqlite_conn = new SQLiteConnection("Data Source= fin.db;Version=3;New=True;Compress=True;")
    sqlite_conn.Open()
    sqlite_conn
let SendToDatabase  (x:SQLiteConnection) (item:FinanceEntry) :unit  = 
    
    
    let insertSql = 
        $"INSERT INTO finance(EntryName, Amount, EntryDate,Category) " + 
        $"""values ("{item.EntryName}",{item.Amount},"{item.EntryDate}","{item.Category}")"""
    let reader =  new SQLiteCommand(insertSql,x) |> fun cmd -> cmd.ExecuteNonQuery()
    ()
    
[<EntryPoint>]
let main argv =
    let conn = db_connect()
    let partial_send = SendToDatabase conn
    let newitem  = {EntryName="1Sauce";Amount=10.50;EntryDate = DateTime.Today;Category= Food}
    let newitem2 = {EntryName="2Sauce";Amount=21.50;EntryDate = DateTime.Today;Category= Lifestyle}
    let newitem3 = {EntryName="3Sauce";Amount=20.50;EntryDate = DateTime.Today; Category = Others}
    (*
    [newitem;newitem2;newitem3] |> List.map partial_send |> ignore 
    *)
    let electionData = 
        [ "Conservative", 306; "Labour", 258; 
                                "Liberal Democrat", 57 ]
    let Bolivia = ["2004/05", 165.; "2005/06", 135.; "2006/07", 157.; "2007/08", 139.; "2008/09", 136.]
    let Ecuador = ["2004/05", 938.; "2005/06", 1120.; "2006/07", 1167.; "2007/08", 1110.; "2008/09", 691.]
    let Madagascar = ["2004/05", 522.; "2005/06", 599.; "2006/07", 587.; "2007/08", 615.; "2008/09", 629.]
    let Average = ["2004/05", 614.6; "2005/06", 682.; "2006/07", 623.; "2007/08", 609.4; "2008/09", 569.6]
    let series = [ "bars"; "bars"; "bars"; "lines" ]
    let inputs = [ Bolivia; Ecuador; Madagascar; Average ]
    let chart2 =
        inputs
        |> Chart.Combo
        |> Chart.WithOptions
            (Options(title = "Coffee Production",
                    series = [| for typ in series -> Series(typ) |]))
        |> Chart.WithLabels ["Bolivia"; "Ecuador"; "Madagascar"; "Average"]
        |> Chart.WithLegend true
        |> Chart.WithSize (600, 250)
        |>Chart.Show
    Console.WriteLine newitem3
    0


