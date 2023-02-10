// For more information see https://aka.ms/fsharp-console-apps
open System
open System.Data.SQLite
open System.Windows.Forms
open Microsoft.FSharp.Core
open DatabaseHelpers
open FinanceTypes
open Microsoft.FSharp.Reflection
open Plotly.NET
open Plotly.NET.ImageExport
open Transactions
open Accounts
open PrettyTable

//TODO 
(*
    Sort by Month, Category, Get Average
    For all expense, print to PDF
    #edit/delete entry

*)
type ('a) FirebaseOperationState =
    | FirebaseOperationSuccess of 'a
    | FirebaseOperationFailure of Exception 

type ProgOptions =
    |AddEntry
    |ViewEntries
    |Goals
    |StatsView
    |Reminder
    |Wishlist
    |ExitProg


type Reminder = {commitment:string;amount:double;date:DateTime;note:string}
//let GenerateRandomUUID () = 
type Goals = {
    id  :string
    goal:string
    amount:double
    current_amount:double
    note:string
    completion_rate:double
    
    
}with
    member this.UpdateGoal (curr_amount:double)=
                { id = this.id
                  goal=this.goal
                  amount = this.amount
                  current_amount = curr_amount
                  note = this.note
                  completion_rate = this.CalculateCompleteRate this.amount curr_amount }
    member this.CalculateCompleteRate amount current_amt =
        (current_amt/amount) * 100.00
        
let ClearScreen () = 
    printfn "Press Any Key To Continue"
    Console.ReadLine() |> ignore
    Console.Clear()

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

let CalculateAmountByCategory alist =
        match alist with
            | [] -> [double (0.00)]
            | _ ->  [for elem in alist do elem.Amount]

let MakeGraph (somelist:FinanceTypes.FinanceEntry list) =
    
    let utilities = List.filter (fun x -> x.Category = Utilities) somelist |> fun x -> CalculateAmountByCategory x |> List.reduce (fun x y -> x + y) 
    let food      = List.filter (fun x -> x.Category = Food)      somelist |> fun x -> CalculateAmountByCategory x |> List.reduce (fun x y -> x + y)
    let lifestyle = List.filter (fun x -> x.Category = Lifestyle) somelist |> fun x -> CalculateAmountByCategory x |> List.reduce (fun x y -> x + y)    
    let others    = List.filter (fun x -> x.Category = Others)    somelist |> fun x -> CalculateAmountByCategory x |> List.reduce (fun x y -> x + y)   
    let data = [
            utilities
            food
            lifestyle
            others
            ]
    let labels = [Utilities.ToString();Food.ToString();Lifestyle.ToString();Others.ToString()]
    
    async{
        let chart =
            
            Chart.Pie (data,Labels=labels)   
            
            |> Chart.withTitle "Monthly Expenses"
           
            |> Chart.withLegend true
            
        chart |> Chart.show
        
        ()
        
    } |> Async.StartImmediate

    ()

let StatsOverview (somelist:FinanceEntry list)=
    
    let list_utilities = List.filter (fun x -> x.Category = Utilities) somelist |> fun filteredList ->  CalculateAmountByCategory filteredList 
    let list_food      = List.filter (fun x -> x.Category = Food)      somelist |> fun filteredList ->  CalculateAmountByCategory filteredList 
    let list_lifestyle = List.filter (fun x -> x.Category = Lifestyle) somelist |> fun filteredList ->  CalculateAmountByCategory filteredList 
    let list_others    = List.filter (fun x -> x.Category = Others)    somelist |> fun filteredList ->  CalculateAmountByCategory filteredList 
    let list_aggreagate     = [list_others;list_food;list_lifestyle;list_utilities]
    let total_amount   = list_aggreagate |> List.map List.sum |> List.reduce (fun x y -> x + y)
    let atable = [["Food";(List.sum list_food).ToString()];
    ["Lifestyle";(List.sum list_lifestyle).ToString()];
    ["Utilities";(List.sum list_utilities).ToString() ];["Others";(List.sum list_others).ToString()];["Total";  (total_amount).ToString()]]
    atable |> prettyTable |> horizontalAlignment FsPrettyTable.Types.Left |> printTable
    printfn "%A MYR is spent on Food"      (List.sum list_food) 
    printfn "%A MYR is spent on Lifestyle" (List.sum list_lifestyle) 
    printfn "%A MYR is spent on Utilities" (List.sum list_utilities) 
    printfn "%A MYR is spent on Others"    (List.sum list_others)
    printfn "Total Spent is %A MYR "       (total_amount)
    let _ = MakeGraph somelist
    // let f = new Form()
    // Application.Run f
    ClearScreen ()
    
    ()

let PrintData (alist:FinanceEntry list):unit =
    match alist with
        | [] -> printfn "No data available"
        | _ ->
    let headers = ["Entry Name";"Amount";"Entry Date"]
    let blist = [for x in alist do [x.EntryName;x.Amount.ToString();x.EntryDate]]
    prettyTable blist |> withHeaders headers |> horizontalAlignment FsPrettyTable.Types.Left |> printTable
    ClearScreen ()
    ()

let PrintReminder alist : unit = 
    match alist with 
        | [] -> printfn "No Reminders/Commitments"
        | _ -> List.iteri (fun i x -> printfn "\n%A.COMMITMENT: %A\nAMOUNT: %A\nDATE: %A\nNOTE: %A" (i+1) x.commitment x.amount x.date x.note ) alist
    ClearScreen ()
    ()
let PrintGoals alist : unit = 
    match alist with 
        | [] -> printfn "No Goals Set"
        | _ -> List.iteri (fun i x -> printfn "\n%A.GOAL: %A\nAMOUNT: %A\nCURRENT AMOUNT: %A\nNOTE: %A\nCOMPLETION RATE: %A" (i+1) x.goal x.amount x.current_amount x.note x.completion_rate ) alist
    ClearScreen ()
    ()

let RetrieveFromDatabase (conn:SQLiteConnection) =
    let querySql = 
        "SELECT * FROM finance"
    use reader = new SQLiteCommand(querySql,conn) |> fun  x  -> x.ExecuteReader()
    let convertStrToDU text =  
        match text with 
            |"Food"      -> Food
            |"Lifestyle" -> Lifestyle
            |"Utilities" -> Utilities
            |"Others"|_  -> Others
    let convertDateTime some_date_time_string = 
        let result = DateTime.Parse some_date_time_string
        result
    let rec ReadRows(rowReader:SQLiteDataReader) (alist: FinanceEntry list) =
        match rowReader.Read() with 
            |false -> alist
            |true ->  
            {
                EntryName=reader.GetString(0);
                Amount     =  (reader.GetDouble 1 ); 
                EntryDate  =  (reader.GetString 2 );
                EntryMonth =  (reader.GetString 2  |> convertDateTime |> fun x-> x.Month);
                EntryYear  =  (reader.GetString 2  |> convertDateTime |> fun x-> x.Year);
                Category   =  (reader.GetString 3  |> convertStrToDU) 
            //applying recursion
            }::alist |> ReadRows rowReader
             
    let resulting_list = ReadRows reader [] |> List.filter (fun x -> x.EntryMonth = DateTime.Now.Month )
    
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
    
let RetriveReminderFromDatabase conn = 
    let querySql = 
        "SELECT * FROM commitments"
    use reader = new SQLiteCommand(querySql,conn) |> fun  x  -> x.ExecuteReader()
    let rec ReadRows(rowReader:SQLiteDataReader) (alist: 'a list) =
        match rowReader.Read() with 
            |false -> alist
            |true ->  
                {
                    commitment =  (reader.GetString 0 );
                    amount     =  (reader.GetDouble 1 ); 
                    date       =  DateTime.Parse(reader.GetString 2 );
                    note       =  (reader.GetString 3 );
                     
                //applying recursion
                }::alist |> ReadRows rowReader

    let resulting_list = ReadRows reader []
    reader.Dispose()
    resulting_list
    
let SendReminderToDatabase conn item = 
    let insertSql = 
        $"INSERT INTO commitments(commitment, amount, date,note) " + 
        $"""values ("{item.commitment}",{item.amount},"{item.date}","{item.note}")"""
    
    let reader = new SQLiteCommand(insertSql,conn) |> fun  x  -> x.ExecuteNonQuery() 
    ()
let SendGoalToDatabase conn goal =
    let insertSql = 
        $"INSERT INTO goals(id,goal, amount, current_amount,note,completion_rate) " + 
        $"""values ("{goal.id}","{goal.goal}",{goal.amount},{goal.current_amount},"{goal.note}",{goal.completion_rate})"""
    
    let reader = new SQLiteCommand(insertSql,conn) |> fun  x  -> x.ExecuteNonQuery() 
    ()
let RetrieveGoalFromDatabase conn =
    let querySql = 
        "SELECT * FROM goals"
    use reader = new SQLiteCommand(querySql,conn) |> fun  x  -> x.ExecuteReader()
    let rec ReadRows(rowReader:SQLiteDataReader) (alist: 'a list) =
        match rowReader.Read() with 
            |false -> alist
            |true ->      {  id    = (reader.GetString 0 );
                           goal =(reader.GetString 1 );
                           amount   = (reader.GetDouble 2 );
                           note =(reader.GetString 4 );
                           current_amount   =(reader.GetDouble 3);
                           completion_rate  = (reader.GetDouble 5); }
                           ::alist |> ReadRows rowReader
                
    let resulting_list = ReadRows reader []
    reader.Dispose()
    resulting_list

let EditGoals()=
    failwith "TODO"
let AddGoals () =
    
    Console.WriteLine "Insert Goals"
    let entry_name = Console.ReadLine()
    Console.WriteLine "Insert Amount"
    let amount = Console.ReadLine()
    Console.WriteLine "Insert Current Amount"
    let current_amount = Console.ReadLine()
    Console.WriteLine "Insert Note"
    let note = Console.ReadLine()
    let new_goal:Goals = { id  = $"goal{DateTime.Now.Millisecond}"
                           goal=entry_name
                           amount= (Double.Parse amount)
                           note= note
                           current_amount=(Double.Parse current_amount)
                           completion_rate = 0.0 }
    new_goal
let AddGoalOperation conn = AddGoals () |> SendGoalToDatabase conn

let GoalsOptions conn = 
    Console.WriteLine "Select Option\n1.View Reminder/Commitments\n2.Add Reminder/Commitments"
    Console.ReadLine() |> function 
        |"1" -> Console.Clear(); RetrieveGoalFromDatabase conn |> PrintGoals 
        |"2" -> Console.Clear(); AddGoalOperation conn |> ignore
        | _ -> ClearScreen()
let ProgramSelect () =
    Console.WriteLine "Finance App\n1.Add Entry\n2.View Monthly Entries\n3.Goals\n4.Chart Your Financial Data\n5.Reminders/Commitments"
    let programmeOp = Console.ReadLine()
                        |> function
                            |"1" -> AddEntry
                            |"2" -> ViewEntries
                            |"3" -> Goals
                            |"4" -> StatsView
                            |"5" -> Reminder
                            |"6"|_ -> ExitProg
    Console.Clear()
    programmeOp


let AddReminder conn = 
    Console.WriteLine "Insert Commitment"
    let entryname = Console.ReadLine()
    Console.WriteLine "Insert Amount"
    let amount = Console.ReadLine()
    Console.WriteLine "Insert Note"
    let note = Console.ReadLine()
    let reminder = {commitment=entryname;amount= double (amount);date=DateTime.Parse(DateTime.Today.ToShortDateString());note=note}
    SendReminderToDatabase conn reminder
    ClearScreen();
    ()
let ReminderOptions conn = 
    Console.WriteLine "Select Option\n1.View Reminder/Commitments\n2.Add Reminder/Commitments"
    Console.ReadLine() |> function 
        |"1" -> Console.Clear(); RetriveReminderFromDatabase conn |> PrintReminder 
        |"2" -> Console.Clear(); AddReminder conn 
        | _ -> ClearScreen()

let rec AddEntryFunc conn : unit =
    
    let checkDouble (valueOf:string) =
            match Double.TryParse(valueOf) with
                |true, x -> Ok x
                |false,x -> Error "Not A Number, Function will called again"
                
    Console.WriteLine "Category\n1.Food\n2.Lifestyle\n3.Utilities\n4.Others"
    let category_val_DU = 
        Console.ReadLine() |> 
        function
            |"1" -> Food
            |"2" -> Lifestyle
            |"3" -> Utilities
            |"4"|_ -> Others
            
    Console.WriteLine "Insert Entry Name"
    let entry_name = Console.ReadLine()
    
    Console.WriteLine "Insert Amount"
    let amount = Console.ReadLine()
    amount
        |> checkDouble
        |> function
            |Ok x -> ()
            |Error s -> Console.WriteLine s
                        AddEntryFunc conn

    let entry_item  = {EntryName=entry_name;Amount= (Double.Parse amount); EntryDate =  DateTime.Today.ToShortDateString();EntryMonth= DateTime.Today.Month;EntryYear= DateTime.Today.Year;Category=category_val_DU }
    SendToDatabase conn entry_item
    let _ = recordTransaction conn (Entry entry_item)
    let _ = entry_item |> transactTypeHelper |> updateBalance MaybankAccount |> recordBalance conn
    ClearScreen ()
    ()

[<EntryPoint>]
let main argv =
//Please apply the Open Late, Close Early Principle for SQLite Connections
    let conn = db_connect ()
    let mutable app_condition = false
    while not app_condition do 
        printfn "FinanceCloud Alpha-0.0.1\nUpcoming Features:\n1.Export to PDF, Excel\n2.Detail Report Generation\n3.GUI\n4.Firebase/Azure Integration\n\r"
        let res = ProgramSelect()
        match res with
            |AddEntry    -> AddEntryFunc conn
            |ViewEntries -> RetrieveFromDatabase conn |> PrintData
            |Goals       -> GoalsOptions conn
            |StatsView   -> RetrieveFromDatabase conn |> StatsOverview
            |Reminder    -> ReminderOptions conn
            |ExitProg    -> app_condition <- true
            
        () 
    let _ = conn.Dispose()
    0


