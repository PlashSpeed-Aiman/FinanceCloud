// For more information see https://aka.ms/fsharp-console-apps
open System
open System.Collections.Generic
open System.Data.SQLite
open System.IO
open System.Threading.Tasks
open Google.Cloud.Firestore.V1
open Microsoft.FSharp.Core
open DatabaseHelpers

open FinanceTypes
open System
open FSharp.Core
open FSharp.Data
open Microsoft.FSharp.Reflection
open XPlot.Plotly
open Google.Cloud.Firestore
open Transactions
open Accounts
//TODO 
(*
    Sort by Month, Category, Get Average
    For all expense, print to PDF


*)
type ('a) FirebaseOperationState =
    | FirebaseOperationSuccess of 'a
    | FirebaseOperationFailure of Exception 

type ProgOptions =
    |AddEntry
    |ViewEntries
    |StatsView
    |ExitProg

let MaybankAccount = {Name = "Maybank";Balance = 200.00}
let asMap (recd:'T) = 
  [ for p in FSharpType.GetRecordFields(typeof<'T>) ->
      p.Name, p.GetValue(recd) ]
  |> Map.ofList

//TODO : For syncing with cloud, first try connection, if failed, keep in queue, allow user to sync to cloud explicitly, if success empty queue, if failed, queue is not emptied
let db_connect () =
    let sqlite_conn = new SQLiteConnection("Data Source= fin.db;Version=3;New=True;Compress=True;")
    sqlite_conn.Open()
    let () = createTableMany sqlite_conn
    sqlite_conn

let GenerateListFromCategory alist =
        match alist with
            | [] -> [double (0.00)]
            | _ ->  [for elem in alist do elem.Amount]

let MakeGraph (somelist:FinanceTypes.FinanceEntry list) =
    
    let utilities = List.filter (fun x -> x.Category = Utilities) somelist |> fun x -> GenerateListFromCategory x |> List.reduce (fun x y -> x + y) |> fun x -> (Utilities.ToString(),x)
    let food      = List.filter (fun x -> x.Category = Food)      somelist |> fun x -> GenerateListFromCategory x |> List.reduce (fun x y -> x + y) |> fun x -> (Food.ToString(),x)
    let lifestyle = List.filter (fun x -> x.Category = Lifestyle) somelist |> fun x -> GenerateListFromCategory x |> List.reduce (fun x y -> x + y) |> fun x -> (Lifestyle.ToString(),x)   
    let others    = List.filter (fun x -> x.Category = Others)    somelist |> fun x -> GenerateListFromCategory x |> List.reduce (fun x y -> x + y) |> fun x -> (Others.ToString(),x)   
    let data = [
            utilities
            food
            lifestyle
            others
            ]
    
    async{
        let chart =
            data
            |> Chart.Pie 
            
            |> Chart.WithTitle "Monthly Expenses"
           
            |> Chart.WithLegend true

        let () = chart.Show()

        ()
    } |> Async.Start

    ()

let StatsOverview (somelist:FinanceEntry list)=
    

    let list_utilities = List.filter (fun x -> x.Category = Utilities) somelist |> fun filteredList ->  GenerateListFromCategory filteredList 
    let list_food      = List.filter (fun x -> x.Category = Food)      somelist |> fun filteredList ->  GenerateListFromCategory filteredList 
    let list_lifestyle = List.filter (fun x -> x.Category = Lifestyle) somelist |> fun filteredList ->  GenerateListFromCategory filteredList 
    let list_others    = List.filter (fun x -> x.Category = Others)    somelist |> fun filteredList ->  GenerateListFromCategory filteredList 
    let list_aggreagate     = [list_others;list_food;list_lifestyle;list_utilities]
    let total_amount   = list_aggreagate |> List.map List.sum |> List.reduce (fun x y -> x + y)
    printfn "%A MYR is spent on Food"      (List.sum list_food) 
    printfn "%A MYR is spent on Lifestyle" (List.sum list_lifestyle) 
    printfn "%A MYR is spent on Utilities" (List.sum list_utilities) 
    printfn "%A MYR is spent on Others"    (List.sum list_others)
    printfn "Total Spent is %A MYR "       (total_amount)
    let _ = MakeGraph somelist
    ()

let PrintData alist :unit = 
    List.iter (fun x -> printfn "ENTRY NAME: %A\nAMOUNT: %A\nENTRY DATE: %A\n" x.EntryName x.Amount x.EntryDate) alist 
    ()

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
    let convertDateTime some_date_time_string = 
        let result = DateTime.Parse some_date_time_string
        result
    let rec ReadRows(rowReader:SQLiteDataReader) (alist: FinanceEntry list) =
        match rowReader.Read() with 
            |false -> alist
            |true ->  {
            EntryName=reader.GetString(0);
            Amount     =  (reader.GetDouble 1 ); 
            EntryDate  =  (reader.GetString 2 );
            EntryMonth =  (reader.GetString 2  |> convertDateTime |> fun x-> x.Month);
            EntryYear  =  (reader.GetString 2  |> convertDateTime |> fun x-> x.Year);
            Category   =  (reader.GetString 3  |> convertStrToDU) }::alist |> ReadRows rowReader
             
    let resulting_list = ReadRows reader [] |> List.filter (fun x -> x.EntryMonth = DateTime.Now.Month - 1)
    
    resulting_list

let SendToDatabase  (conn:SQLiteConnection) (item:FinanceEntry) :unit  = 
    
    let insertSql = 
        $"INSERT INTO finance(entry_name, amount, entry_date,category) " + 
        $"""values ("{item.EntryName}",{item.Amount},"{item.EntryDate}","{item.Category}")"""
    
    let reader = new SQLiteCommand(insertSql,conn) |> fun  x  -> x.ExecuteNonQuery() 
    
    let input:Map<string,obj> = FSharp.Collections.Map [ ("EntryName", item.EntryName); ("Amount", item.Amount);("EntryDate",item.EntryDate);("EntryMonth",item.EntryMonth);("EntryYear",item.EntryYear);("Category",item.Category.ToString())]
    
    (*let db = FirestoreDb.Create("") |> fun db -> db.Collection("finance")
   
    let sendHelper  =
        async{
          let x = db.AddAsync(input)
          ()
        }
        //TODO : ERROR HANDLING
    sendHelper  |> Async.Start*)
    
    ()
    
let ProgramSelect () =
    Console.WriteLine "Finance App\n1.Add Entry\n2.View Monthly Entries\n3.Chart Your Financial Data"
    let value = Console.ReadLine()
    let helperfunc value =
        match value with
            |"1" -> AddEntry
            |"2" -> ViewEntries
            |"3" -> StatsView
            |"4"|_ -> ExitProg
    let progOp = helperfunc value
    progOp

let AddEntryFunc conn =
    Console.WriteLine "Insert Entry Name"
    let entryname = Console.ReadLine()
    Console.WriteLine "Insert Amount"
    let amount = Console.ReadLine()
    Console.WriteLine "Category\n1.Food\n2.Lifestyle\n3.Utilities\n4.Others"
    let category_val_DU = 
        Console.ReadLine() |> 
        function
            |"1" -> Food
            |"2" -> Lifestyle
            |"3" -> Utilities
            |"4"|_ -> Others
        
(*    let helperfunc category_val =
        match category_val with
            |"1" -> Food
            |"2" -> Lifestyle
            |"3" -> Utilities
            |"4"|_ -> Others
    
    let category_val_DU = helperfunc category_val*)
    let entry_item  = {EntryName=entryname;Amount= (Double.Parse amount); EntryDate =  DateTime.Today.ToShortDateString();EntryMonth= DateTime.Today.Month;EntryYear= DateTime.Today.Year;Category=category_val_DU }
    printfn "AMOUNT : %A" entry_item.Amount
    SendToDatabase conn entry_item
    let _ = recordTransaction conn (Entry entry_item)
    let _ = entry_item |> transactTypeHelper |> updateBalance MaybankAccount |> recordBalance conn
    ()

[<EntryPoint>]
let main argv =
//Please apply the Open Late, Close Early Principle for SQLite Connections
    let conn = db_connect ()
    let mutable app_condition = false
    while not app_condition do 
        printfn "\nFinanceCloud Alpha-0.0.1\nUpcoming Features:\n1.Export to PDF, Excel\n2.Detail Report Generation\n3.GUI\n4.Firebase/Azure Integration\n\r"
        let res = ProgramSelect()
        match res with
            |AddEntry    -> AddEntryFunc conn
            |ViewEntries -> RetrieveFromDatabase conn |> PrintData
            |StatsView   -> RetrieveFromDatabase conn |> StatsOverview
            |ExitProg    -> app_condition <- true
            
        () 
    let _ = conn.Dispose()
    0


