module StufflyToGambitConverter.Converter.Tests.Unit

open System
open Program
open NUnit.Framework
open FsUnit

[<TestFixture>]
type ProgramTests() =
    let parseSingleLineStufflyFile line =
        let items = snd (parseStufflyFile ("", [line]))
        items.Length |> should be (lessThanOrEqualTo 1)
        items

    [<Test>]
    member this.``parseStufflyFile parses single line empty string into empty document``() =
        parseSingleLineStufflyFile "" |> should equal []

    [<Test>]
    member this.``parseStufflyFile parses single line whitespaces only into empty document``() =
        parseSingleLineStufflyFile " " |> should equal []
        parseSingleLineStufflyFile "  " |> should equal []
        parseSingleLineStufflyFile "\t" |> should equal []
        parseSingleLineStufflyFile "\t\t" |> should equal []
        parseSingleLineStufflyFile "\t " |> should equal []

    [<Test>]
    member this.``parseStufflyFile parses single line date into empty document``() =
        parseSingleLineStufflyFile "2017.08.26" |> should equal []

    [<Test>]
    member this.``parseStufflyFile parses single line date with whitespaces into empty document``() =
        parseSingleLineStufflyFile "2017.08.26 " |> should equal []
        parseSingleLineStufflyFile "2017.08.26  " |> should equal []
        parseSingleLineStufflyFile "2017.08.26\t" |> should equal []
        parseSingleLineStufflyFile "2017.08.26\t\t" |> should equal []
        parseSingleLineStufflyFile "2017.08.26\t " |> should equal []

    [<Test>]
    member this.``parseStufflyFile parses single line vertical line into empty document``() =
        parseSingleLineStufflyFile "|" |> should equal []

    [<Test>]
    member this.``parseStufflyFile parses single line text into left part with that text``() =
        fst (parseSingleLineStufflyFile "this is some simple text").Head |> should equal "this is some simple text"

    [<Test>]
    member this.``parseStufflyFile removes the date``() =
        fst (parseSingleLineStufflyFile "2017.08.26 this is some simple text").Head |> should equal "this is some simple text"

    [<Test>]
    member this.``parseStufflyFile trims the left part``() =
        fst (parseSingleLineStufflyFile " \tthis is some simple text \t").Head |> should equal "this is some simple text"

    [<Test>]
    member this.``parseStufflyFile trims the right part``() =
        snd (parseSingleLineStufflyFile "| \tthis is some simple text \t").Head |> should equal "this is some simple text"

    [<Test>]
    member this.``parseStufflyFile removes tags``() =
        fst (parseSingleLineStufflyFile "this is some simple text #tag1 #tag2 #tag-with-dash").Head |> should equal "this is some simple text"

    [<Test>]
    member this.``parseStufflyFile removes sources``() =
        fst (parseSingleLineStufflyFile "this is some simple text @source1 @source2 @source-with-dash").Head |> should equal "this is some simple text"

    [<Test>]
    member this.``parseStufflyFile does not remove hashes within a word``() =
        fst (parseSingleLineStufflyFile "www.somewebsite.com/#anchor").Head |> should equal "www.somewebsite.com/#anchor"

    [<Test>]
    member this.``parseStufflyFile creates the right part by shuffling the left part if the right part is not provided``() =
        let parsedDocument = parseSingleLineStufflyFile "this is some simple text"
        let leftSide = fst parsedDocument.Head
        let rightSide = snd parsedDocument.Head
        leftSide |> should equal "this is some simple text"
        rightSide |> should startWith "<<"
        rightSide |> should endWith ">>"
        rightSide.TrimStart('<').TrimEnd('>') |> should not' (equal "this is some simple text") 
        "this is some simple text".Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.iter (fun word -> rightSide |> should haveSubstring word)

    [<Test>]
    member this.``parseStufflyFile properly parses common real-life examples``() =
        let isShuffledVersionOf (original : string ) (shuffled : string) =
            let trimmedShuffled = shuffled.TrimStart('<').TrimEnd('>')
            original.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
                |> Array.forall (fun word -> trimmedShuffled.Contains(word))

        parseSingleLineStufflyFile "2015.09.18 -r Richtungswechsel,-|the change of direction" |> should equal [("-r Richtungswechsel,-", "the change of direction")]

        fst (parseSingleLineStufflyFile "2017.08.22 es ist mehr im Weg als es hilft").Head |> should equal "es ist mehr im Weg als es hilft"
        isShuffledVersionOf "es ist mehr im Weg als es hilft" (snd (parseSingleLineStufflyFile "2017.08.22 es ist mehr im Weg als es hilft").Head) |> should be True

        fst (parseSingleLineStufflyFile "FSharp Language Overview [T. Petricek; -; -]").Head |> should equal "FSharp Language Overview [T. Petricek; -; -]"
        isShuffledVersionOf "FSharp Language Overview [T. Petricek; -; -]" (snd (parseSingleLineStufflyFile "FSharp Language Overview [T. Petricek; -; -]").Head) |> should be True

        parseSingleLineStufflyFile "2017.08.22 fssnip.net #fsharp|F# snippets." |> should equal [("fssnip.net", "F# snippets.")]

        parseSingleLineStufflyFile "2017.07.14 monitorbacklinks.com/seo-tools/free-backlink-checker #seo #backlinks|Free Backlings Checker." |> should equal [("monitorbacklinks.com/seo-tools/free-backlink-checker", "Free Backlings Checker.")]

        parseSingleLineStufflyFile "2017.03.11 decksetapp.com #markdown #presentation #mac @visnja-zeljeznjak|Deckset." |> should equal [("decksetapp.com", "Deckset.")]

    [<Test>]
    // Covers https://github.com/ironcev/stuffly-to-gambit-converter/issues/1
    // Shuffled front pages have leading space after the <<
    member this.``parseStufflyFiles creates a shuffled right side that do not have leading space after the <<``() =
        let rightSide = snd (parseSingleLineStufflyFile "this is some text").Head
        rightSide.TrimStart('<').TrimEnd('>') |> should not' (startWith " ")
        let rightSide = snd (parseSingleLineStufflyFile "2017.08.27 this is some text").Head
        rightSide.TrimStart('<').TrimEnd('>') |> should not' (startWith " ")
        