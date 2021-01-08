﻿namespace Commod
[<AutoOpen>]
module Pricer =
    open System
    open MathNet.Numerics
    open MathNet.Numerics.LinearAlgebra
    open MathNet.Numerics.Statistics

    let SwapPricer inst d1 d2 (f:PriceCurve) = //std swap pricer 
        let c = getCommod inst
        let s = getSwap inst d1 d2 ( c.LotSize ) ( c.Quotation * 0.M) 
        let a = priceSwap s f
        a.Value / s.Quantity.Value  |> float


    // get equal weights based on the number of fixings
    let getEqualWeights x =
        let n = Array.length x
        let w = 1.0 / float n 
        Array.replicate n w 

    let inline toVector s = s |> Array.map float |> vector 

    /// split fixings into future and past using pricingDate
    let splitDetails pricingDate details = 
        let n = details |> Array.tryFindIndex( fun (x,_,_) -> x > pricingDate ) 
        match n with
        | Some 0 -> (Array.empty, details)
        | Some i -> Array.splitAt i details
        | None -> (details, Array.empty)

    ///take refmonth and return array of tuple: 
    ///fixingdate, weight, contract, 
    ///slope is applied here into the weights.
    let getFixings refMonth (com:Commod) lags slope avg expDate =     
        let refDate = refMonth |> pillarToDate 
        //get reference contract, swap for oil, bullet for gas
        let avgfwd = getAvgFwd com.Instrument
        let contracts' = getNrbyContracts avgfwd
        lags 
        |> Array.map( fun i -> 
            let refMonth = refDate.AddMonths i 
            let contract = refMonth |> formatPillar
            let d1,d2 = 
                match com.Instrument with 
                | JKM | TTF | NG -> //for gas, use the contract month
                    getContractMonth contracts' contract
                | _ ->   refMonth, dateAdjust' "e" refMonth
            //let dates = getFixingDates avgfwd.Frequency com.Calendar d1 d2 
            let dates = getFixingDates avg com.Calendar d1 d2 
            //let contracts = List.replicate dates.Length contract
            let contracts = getFixingContracts contracts' dates
            let weights = (getEqualWeights dates) |> Array.map( fun x -> x /(float lags.Length) * (float slope))
            let fixingDates = dates |> Array.map( fun d -> min d expDate)
            fixingDates, weights, contracts
            )
        |> Array.reduce( fun ( d1,w1,c1) (d2,w2,c2) -> 
            (Array.append d1 d2), 
            (Array.append w1 w2),
            (Array.append c1 c2))
        //consolidate future details to group weightes for same fixing dates and same contracts
        |||> Array.zip3
        |> Array.groupBy(fun (x,_,z) -> x,z) |> Array.map( fun ((k1,k2),v) -> k1,(v |> Array.sumBy( fun (_,x,_)->x)),k2)

    ///generate inputs for option pricing 
    /// inputs are
    /// futureDetails is a list of tuple of fixingdate, weight, ContractPillar 
    /// getPriceFunc take contractPillar and return price 
    /// getVolFunc take contractPillar and return vol
    /// return tuple of 4 vectors:
    /// forwards, weights, time to maturity, vols
    let getFutureInputs futureDetails getPriceFunc getVolFunc =
        //consolidate future details to group weightes for same fixing dates and same contracts
        let (t1,fw1,contracts) = futureDetails |> Array.unzip3 
        let f1 = contracts |> Array.map getPriceFunc 
        let v1 = contracts |> Array.map getVolFunc 
        (f1, fw1, t1, v1)

    ///generate past average required for asian option
    let getPastInputs pastDetails getFixingFunc = 
        pastDetails |> Array.map(fun ( d, w, c ) -> float (getFixingFunc d c) * w ) |> Array.sum

    //for the final portfolio we need just functions that take price curve and return price. 
    let getInputs pricingDate expDate refMonth lags avg inst slope (pricecurve:PriceCurve) (volcurve:VolCurve) priceOverride volOverride = 
        let com = getCommod inst
        let getPrices1 c = 
           match priceOverride with
           | Some v -> v
           | None ->
                if pricecurve.Pillars.Contains c then
                    (pricecurve.Item c).Value
                else
                    failwithf "try to getPrice:%s from %A" c pricecurve
        let getVol c = 
           match volOverride with
           | Some v -> v
           | None -> 
                if volcurve.Pillars.Contains c then
                    volcurve.Item c
                else
                    failwithf "try to get vol:%s from %A" c volcurve

        let (pastDetails1, futureDetails1 ) = splitDetails pricingDate ( getFixings refMonth com lags slope avg expDate )
        let (f1, fw1, d1, v1 ) = getFutureInputs futureDetails1 getPrices1 getVol 
        let p1 = getPastInputs pastDetails1 (fun _ c -> getPrices1 c ) 
        let t1 = d1 |> Array.map (getTTM pricingDate)
        ( toVector f1,
          toVector fw1,
          toVector t1,
          toVector v1,
          p1)

        ///spread option of inst1 (e.g DBRT ) vs inst2 (e.g. JKM) 
    let SpreadOptionPricerBS inst1 start1 end1 avg1 inst2 start2 end2 avg2 slope freight callput expDate  
        refMonth (pricingDate:DateTime)
        rho pricecurve1 volcurve1 pricecurve2 volcurve2 price1 vol1 price2 vol2 =
        let lags1 = [|start1 .. end1|]
        let lags2 = [|start2 .. end2|]
        let optPricer inst1 inst2 rho refMonth = 
            //let rho = 0.4
            //let refMonth = "Dec20"
            //let freight = if refMonth = "JUL-21" then 0.85 else 1.0
            let (f1,fw1,t1,v1,a1) = getInputs pricingDate expDate refMonth lags1 avg1 inst1 slope pricecurve1 volcurve1 price1 vol1
            let (f2,fw2,t2,v2,a2) = getInputs pricingDate expDate refMonth lags2 avg2 inst2 1.0M pricecurve2 volcurve2 price2 vol2
            // put optionality:
            // exercise when JCC + freight < JKM, => -freight - ( JCC - JKM) > 0
            // so a put on ( JCC - JKM ) with strike  = -freight 
            let k = -freight - a1 + a2 /// adapte K for past fixings
            //let opts = spreadoption f1 fw1 t1 v1 f2 fw2 t2 v2 k rho callput p1 pw1 p2 pw2
            ////printfn "%A %A %A %A %A %A %A %A %f" f1 fw1 t1 v1 f2 fw2 t2 v2 rho
            let opt, deltas =  optionChoi2Asset' f1 fw1 t1 v1 f2 fw2 t2 v2 k rho callput
            let p1 = ((f1 .* fw1 ).Sum() + freight) + a1  //inst1 forwd
            let p2 = ((f2 .* fw2 ).Sum())+ a2 //inst2 fwd
            let pintr = 
                match callput with 
                | Call -> (max (p1 - p2) 0.)
                | Put -> (max (p2 - p1) 0.)

            let deltaA = deltas
            [|   "Option", opt;
                "Delta1", deltaA.[0];
                "Delta2", deltaA.[1];
                "P1", p1;
                "P2", p2;
                "Intrinsic", pintr;
                "vol1", ( Statistics.Mean v1); //vol1 
                "vol2", ( Statistics.Mean v2); //vol1 
            |]

        optPricer inst1 inst2 rho refMonth

    let getPricesWithOverride (crv:PriceCurve) p c = 
       match p with
       | Some v -> v
       | None ->
            if crv.Pillars.Contains c then
                (crv.Item c).Value
            else
                failwithf "try to getPrice:%s from %A" c crv

    ///spread option using Gabillon model
    let SpreadOptionPricerGabillon inst1 start1 end1 avg1 inst2 start2 end2 avg2 slope freight callput expDate  
        refMonth (pricingDate:DateTime)
        rho pricecurve1 volcurve1 pricecurve2 volcurve2 price1 vol1 price2 vol2 =
        let lags1 = [|start1 .. end1|]
        let lags2 = [|start2 .. end2|]

        let getInputsG pricingDate expDate refMonth lags1 avg1 inst1 slope (pricecurve1:PriceCurve) volcurve1 price1 vol1 exp = 
            let com1 = getCommod inst1
            let getPrices1 = getPricesWithOverride pricecurve1 price1 
            let (pastDetails1, futureDetails1 ) = splitDetails pricingDate ( getFixings refMonth com1 lags1 slope avg1 expDate )           
            let fixings1 = futureDetails1 |> Array.map( fun (x,_,y) -> (min x expDate),y)
            let fw1 = futureDetails1 |> Array.map( fun (_,w,_) -> w) |> toVector
            let sigma1 = getGabillonCov inst1 volcurve1 (getGabillonParam inst1) fixings1 pricingDate 
            let t1 = fixings1 |> Array.unzip |> fst |> Array.map (getTTM pricingDate) |> toVector
            let f1 = fixings1 |> Array.map( fun (_,c) -> getPrices1 c) |> toVector
            let a1 = getPastInputs pastDetails1 (fun _ c -> getPrices1 c )
            (f1,fw1,t1,sigma1,a1)

        let (f1,fw1,t1,v1,a1) = getInputsG pricingDate expDate refMonth lags1 avg1 inst1 slope pricecurve1 volcurve1 price1 vol1 expDate
        let (f2,fw2,t2,v2,a2) = getInputsG pricingDate expDate refMonth lags2 avg2 inst2 1.0M pricecurve2 volcurve2 price2 vol2 expDate
        let k = -freight - a1 + a2 /// adapte K for past fixings
        //let opt, deltas =  optionChoi2AssetCov f1 fw1 t1 v1 f2 fw2 t2 v2 k rho callput //cov breakdown too often
        let v1' = ( v1.Diagonal() ./ t1 ).PointwiseSqrt()
        let v2' = ( v2.Diagonal() ./ t2 ).PointwiseSqrt()
        let opt, deltas =  optionChoi2Asset' f1 fw1 t1 v1' f2 fw2 t2 v2' k rho callput
        let p1 = ((f1 .* fw1 ).Sum() + freight) + a1  //inst1 forwd
        let p2 = ((f2 .* fw2 ).Sum())+ a2 //inst2 fwd
        let pintr = 
            match callput with 
            | Call -> (max (p1 - p2) 0.)
            | Put -> (max (p2 - p1) 0.)

        let deltaA = deltas
        [|   "Option", opt;
            "Delta1", deltaA.[0];
            "Delta2", deltaA.[1];
            "P1", p1;
            "P2", p2;
            "Intrinsic", pintr;
            "vol1", ( Statistics.Mean (v1.Diagonal() ./ t1 |> Vector.Sqrt ) ); //vol1 
            "vol2", ( Statistics.Mean (v2.Diagonal() ./ t2 |> Vector.Sqrt ) ); //vol1 
        |]

    ///spread option using cross Gabillon model
    let SpreadOptionPricerXGabillon inst1 start1 end1 avg1 inst2 start2 end2 avg2 slope freight callput expDate  
        refMonth (pricingDate:DateTime)
        rho pricecurve1 volcurve1 pricecurve2 volcurve2 price1 vol1 price2 vol2 =
        let lags1 = [|start1 .. end1|]
        let lags2 = [|start2 .. end2|]

        let getInputsG pricingDate expDate refMonth lags1 avg1 inst1 slope (pricecurve1:PriceCurve) price1 = 
            let com1 = getCommod inst1
            let getPrices1 = getPricesWithOverride pricecurve1 price1 
            let (pastDetails1, futureDetails1 ) = splitDetails pricingDate ( getFixings refMonth com1 lags1 slope avg1 expDate )           
            let fixings1 = futureDetails1 |> Array.map( fun (x,_,y) -> (min x expDate),y)
            let fw1 = futureDetails1 |> Array.map( fun (_,w,_) -> w) |> toVector
            let f1 = fixings1 |> Array.map( fun (_,c) -> getPrices1 c) |> toVector
            let a1 = getPastInputs pastDetails1 (fun _ c -> getPrices1 c )
            (f1,fw1,fixings1,a1)

        let (f1,fw1,x1,a1) = getInputsG pricingDate expDate refMonth lags1 avg1 inst1 slope pricecurve1 price1
        let (f2,fw2,x2,a2) = getInputsG pricingDate expDate refMonth lags2 avg2 inst2 1.0M pricecurve2 price2 
        let k = -freight - a1 + a2 /// adapte K for past fixings
        //let opt, deltas =  optionChoi2AssetCov f1 fw1 t1 v1 f2 fw2 t2 v2 k rho callput //cov breakdown too often
        let xParam = getXGabillonParam inst1 inst2
        let sigma = getXGabillonCovFull inst1 volcurve1 x1 inst2 volcurve2 x2 xParam pricingDate 
        let t1 = x1 |> Array.map (fst >> getTTM pricingDate ) |> toVector
        let t2 = x2 |> Array.map (fst >> getTTM pricingDate) |> toVector
        let n = f1.Count
        let opt, deltas =  optionChoi2AssetCov f1 fw1 t1 f2 fw2 t2 k sigma callput
        let p1 = ((f1 .* fw1 ).Sum() + freight) + a1  //inst1 forwd
        let p2 = ((f2 .* fw2 ).Sum())+ a2 //inst2 fwd
        let pintr = 
            match callput with 
            | Call -> (max (p1 - p2) 0.)
            | Put -> (max (p2 - p1) 0.)

        let v1 = ( sigma.Diagonal().[0..(f1.Count-1)] ./ t1 ).PointwiseSqrt()
        let v2 = ( sigma.Diagonal().[f1.Count..] ./ t2 ).PointwiseSqrt()
        let deltaA = deltas
        [|   "Option", opt;
            "Delta1", deltaA.[0];
            "Delta2", deltaA.[1];
            "P1", p1;
            "P2", p2;
            "Intrinsic", pintr;
            "vol1", ( Statistics.Mean v1 ); //vol1 
            "vol2", ( Statistics.Mean v2 ); //vol1 
        |]

