// For more information see https://aka.ms/fsharp-console-apps
open System
open System.Collections.Generic
open System.Data.SQLite
open System.IO
open System.Threading.Tasks
open Google.Cloud.Firestore.V1
open Microsoft.FSharp.Core
(*
    Sort by Month, Category, Get Average
    For all expense, print to PDF


*)
open System
open FSharp.Core
open FSharp.Data
open Microsoft.FSharp.Reflection
open XPlot.GoogleCharts
open Google.Cloud.Firestore

type ('a) FirebaseOperationState =
    | FirebaseOperationSuccess of 'a
    | FirebaseOperationFailure of Exception 
type FinanceCategories =
    | Food
    | Lifestyle
    | Utilities
    | Others
type ProgOptions =
    |AddEntry
    |ViewEntries
    |ExitProg

type FinanceEntry =
    {EntryName:string;Amount:double;EntryDate:string;EntryMonth:int;EntryYear:int;Category:FinanceCategories}

let asMap (recd:'T) = 
  [ for p in FSharpType.GetRecordFields(typeof<'T>) ->
      p.Name, p.GetValue(recd) ]
  |> Map.ofList
let getAmount item = item.Amount
//TODO : For syncing with cloud, first try connection, if failed, keep in queue, allow user to sync to cloud explicitly, if success empty queue, if failed, queue is not emptied
let db_connect () =
    let sqlite_conn = new SQLiteConnection("Data Source= fin.db;Version=3;New=True;Compress=True;")
    sqlite_conn.Open()
    sqlite_conn

let RetrieveFromDatabase (conn:SQLiteConnection) =
    let querySql = 
        "SELECT * FROM finance"
    use reader = new SQLiteCommand(querySql,conn) |> fun  x  -> x.ExecuteReader()
    let convertStrToDU text =  
        match text with 
            |"Food" -> Food
            |"Lifestyle" -> Lifestyle
            |"Utilities" -> Utilities
            |"Others"|_ -> Others
    let rec readHelper (reader:SQLiteDataReader) (alist: FinanceEntry list) =
        match reader.Read() with 
            |false -> alist
            |true ->  {EntryName=reader.GetString(0);Amount= (reader.GetDouble 1 ); EntryDate =  (reader.GetString(2));EntryMonth= DateTime.Today.Month;EntryYear= DateTime.Today.Year;Category=(reader.GetString(3) |> convertStrToDU) }::alist |> readHelper reader
                 
    let res = readHelper reader []
    List.iter (fun x -> printfn "%A" x) res 
    let filteres = List.filter (fun x -> x.Category = Lifestyle) res |> fun x -> [ for elem in x do elem.Amount]
    printfn "%A MYR is spent on Lifestyle" (List.sum filteres) 
    ()

let SendToDatabase  (conn:SQLiteConnection) (item:FinanceEntry) :unit  = 
    
    let insertSql = 
        $"INSERT INTO finance(entry_name, amount, entry_date,category) " + 
        $"""values ("{item.EntryName}",{item.Amount},"{item.EntryDate}","{item.Category}")"""
    
    let reader = new SQLiteCommand(insertSql,conn) |> fun  x  -> x.ExecuteNonQuery() 
    let input:Map<string,obj> = FSharp.Collections.Map [ ("EntryName", item.EntryName); ("Amount", item.Amount);("EntryDate",item.EntryDate);("EntryMonth",item.EntryMonth);("EntryYear",item.EntryYear);("Category",item.Category.ToString())]
    let db = FirestoreDb.Create("shining-weft-357007") |> fun db -> db.Collection("finance")
   
    let sendHelper  =
        async{
          let x = db.AddAsync(input)
          ()
        }
        //TODO : ERROR HANDLING
    sendHelper  |> Async.Start
    
    ()
    
let ProgramSelect () =
    Console.WriteLine "Finance App\n1.Add Entry\n2.View Monthly Entries"
    let value = Console.ReadLine()
    let helperfunc value =
        match value with
            |"1" -> AddEntry
            |"2" -> ViewEntries
            |"3"|_ -> ExitProg
    let res = helperfunc value
    res

let AddEntryFunc conn =
    Console.WriteLine "Insert Entry Name"
    let entryname = Console.ReadLine()
    Console.WriteLine "Insert Amount"
    let amount = Console.ReadLine()
    Console.WriteLine "Category\n1.Food\n2.Lifestyle\n3.Utilities\n4.Others"
    let category_val = Console.ReadLine()
    let helperfunc category_val =
        match category_val with
            |"1" -> Food
            |"2" -> Lifestyle
            |"3" -> Utilities
            |"4"|_ -> Others
    let category_val_DU = helperfunc category_val
    let entry_item  = {EntryName=entryname;Amount= (Double.Parse amount); EntryDate =  DateTime.Today.ToShortDateString();EntryMonth= DateTime.Today.Month;EntryYear= DateTime.Today.Year;Category=category_val_DU }
    SendToDatabase conn entry_item
    ()

[<EntryPoint>]
let main argv =
//Please apply the Open Late, Close Early Principle for SQLite Connections
    let conn = db_connect()
    let partial_send = SendToDatabase conn
    let newitem  = { EntryName="1Sauce"
                     Amount=10.50
                     EntryDate = DateTime.Today.ToShortDateString()
                     EntryMonth=DateTime.Today.Month
                     EntryYear=DateTime.Today.Year
                     Category = Food }
    let newmapitem = asMap newitem
    let mutable app_condition = false
    while not app_condition do 
        let res = ProgramSelect()
        match res with
            |AddEntry -> AddEntryFunc conn
            |ViewEntries -> RetrieveFromDatabase conn
            |ExitProg -> app_condition <- true
        () 
    let _ = conn.Dispose()
    
    printfn "%A" (newmapitem)
    0


