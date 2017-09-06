// Let's clarify the terminology.
// The folder that has to be converted is called Stuffly Folder.
// Every non-empty textual file in it is called Stuffly File.
// Every line in a Stuffly File is called Stuffly Item.
// Stuffly Item can have two Parts separated by a vertical line (|).
// Parts are called Left Part and Right Part.
// Once parsed, a Stuffly File is called Stuffly Document.
// Each Stuffly File is converted to a Gambit Deck.
// Each Stuffly Item in the file is converted to a Card within the Deck.
// Each Card has Front Page Side and a Back Page Side.
// The left part of a Stuffly Item is mapped to the Back Page Side.
// The right part of a Stuffly Item is mapped to the Front Page Side.

open System.IO

let getStufflyFolder argv =
    match argv with
        | [] -> Directory.GetCurrentDirectory()
        | directory::_ -> directory

let getStufflyFilesWithin stufflyFolder =
    (new DirectoryInfo(stufflyFolder)).EnumerateFiles("*.txt", SearchOption.AllDirectories)
        |> Seq.map (fun file -> file.FullName)

let readStufflyFile filePath =
    (
        Path.GetFileNameWithoutExtension(filePath),
        File.ReadAllLines(filePath) |> Seq.ofArray
    )

open System
open System.Text.RegularExpressions

let parseStufflyFile (fileName : string, fileContent) =
    let parseStufflyItem (item : string) =
        
        let removeDateTagsAndSources =
            let regex = new Regex("^\d\d\d\d\.\d\d\.\d\d|\s#[\w-]+|\s@[\w-]+")
            let removeDateTagsAndSourcesImplementation = fun (leftPart, rightPart) -> (regex.Replace(leftPart, ""), rightPart)
            removeDateTagsAndSourcesImplementation

        let trimParts (leftPart : string, rightPart : string) =
            (leftPart.Trim(), rightPart.Trim())

        let createRightPartIfEmpty (leftPart, rightPart) =
            let shuffle (text : string) =
                    let rec swapArrayElementsWithDifferentElementInPlace currentElementIndex (array : string[]) =
                        let indexOfTheFirstDifferentElement = 
                            match Array.tryFindIndex (fun element -> element <> array.[currentElementIndex]) array with
                                | None -> currentElementIndex
                                | Some(index) -> index
                        if currentElementIndex <> indexOfTheFirstDifferentElement then
                            let temp = array.[currentElementIndex]
                            array.[currentElementIndex] <- array.[indexOfTheFirstDifferentElement]
                            array.[indexOfTheFirstDifferentElement] <- temp
    
                        if currentElementIndex < array.Length - 1 then
                            swapArrayElementsWithDifferentElementInPlace (currentElementIndex + 1) array


                    let splittedWords = text.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)                    
                    match splittedWords.Length with
                        | 0 -> ""
                        | 1 -> splittedWords.[0] // If the left part has just a single word...
                            // ...then we have to return that word.
                            // We cannot return the original 'text' here because it could be that
                            // it had leading or trailing spaces that are removed when the word was
                            // split.
                        | 2 -> String.Concat(splittedWords.[1], " ", splittedWords.[0]) // Just swap the words.
                        | _ -> 
                            // Only if not all the words are same...
                            if splittedWords |> Array.exists (fun element -> element <> splittedWords.[0])
                            then  
                                splittedWords |> (swapArrayElementsWithDifferentElementInPlace 0) //...  shuffle the array in place.

                            String.Join(" ", splittedWords)

            if String.IsNullOrWhiteSpace rightPart && not (String.IsNullOrWhiteSpace leftPart) then
                (leftPart, sprintf "<<%s>>" (shuffle leftPart))
            else
                (leftPart, rightPart)

        let parts =
            let indexOfSeparator = item.IndexOf('|')
            match indexOfSeparator with
                | -1 -> (item, "")
                | index ->
                    (
                        item.Substring(0, index), // This could be an empty string.
                        // We can have a situation when the item contains only the separator "|"
                        // or the separator is the last character ;-)
                        if index = item.Length - 1  then "" else item.Substring(index + 1)
                    ) 

        parts
            |> removeDateTagsAndSources
            |> createRightPartIfEmpty
            |> trimParts

    let parsedDocument =
        fileContent
            |> Seq.map parseStufflyItem
            |> Seq.where (fun parts -> parts <> ("", "") )
    (fileName, parsedDocument)


open FSharp.Data.Sql
open System.Reflection

// SalDataProvider type provider plays well with the SQLite NuGet package. For more info see the below links:
//      http://blog.wezeku.com/2016/12/17/f-sqlprovider-and-sqlite-now-work-better-together/
//      https://system.data.sqlite.org/index.html/tktview/d4728aecb76adffb227e1bfd5350e81f3cbee7a7
let [<Literal>] resolutionPath = __SOURCE_DIRECTORY__ + @"..\..\packages\System.Data.SQLite.Core.1.0.105.2\lib\net46"
let [<Literal>] connectionString = "Data Source=" + __SOURCE_DIRECTORY__ + @"\GambitDatabase.db;Version=3"

type GambitDatabase = SqlDataProvider< 
                            ConnectionString = connectionString,
                            DatabaseVendor = Common.DatabaseProviderTypes.SQLITE,
                            ResolutionPath = resolutionPath,
                            IndividualsAmount = 1000,
                            UseOptionTypes = true>


let createOutputDatabaseWithin stufflyFolder =    
    let outputDatabaseFileName = Path.Combine(stufflyFolder, "Output.db")
    if File.Exists(outputDatabaseFileName) then
        File.Delete(outputDatabaseFileName)

    File.Copy(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "GambitDatabase.db"), outputDatabaseFileName)

    sprintf "Data Source=%s;Version=3" outputDatabaseFileName


let createDeck (database : GambitDatabase.dataContext) (documentName, parts) =
    let newDeck = database.Main.Decks.Create()
    newDeck.Title <- documentName
    newDeck.CurrentCardIndex <- 0
    (newDeck, parts)


let createDeckCards (database : GambitDatabase.dataContext) (deckId, parts) =
    let createNewCard (database : GambitDatabase.dataContext) index deckId (left, right) =
        let newCard = database.Main.Cards.Create()
        newCard.DeckId <- deckId
        newCard.FrontPageSide <- right
        newCard.BackPageSide <- left
        newCard.OrderIndex <- index
        ()

    parts |> Seq.iteri (fun index part -> createNewCard database index deckId part)

[<EntryPoint>]
let main argv = 

    let stufflyFolder = getStufflyFolder (Array.toList argv)
    
    let stufflyDocuments = 
        getStufflyFilesWithin stufflyFolder
            |> Seq.map (readStufflyFile >> parseStufflyFile)
            |> Seq.where (fun (documentName, documentContent) -> not (Seq.isEmpty documentContent))

    let outputDatabaseConnectionString = createOutputDatabaseWithin stufflyFolder

    let database = GambitDatabase.GetDataContext(outputDatabaseConnectionString)

    let decks =
        stufflyDocuments
            |> Seq.map (createDeck database)
            |> Seq.toList // We need the list here in order not to reenumerate the sequence below when we create cards.

    // We have to save the decks so that they get the IDs updated before we create their cards.
    database.SubmitUpdates()

    decks
        |> List.map (fun deck -> ((fst deck).Id, snd deck))        
        |> List.iter (createDeckCards database)

    database.SubmitUpdates()

    0