Project outline
========================

The primary objective of this project is to provide an open-source reference for quantitative functions related to commodities. It includes a commodity pricing library [CommodQuant](https://github.com/xqguo/CommodQuant), with its azure DevOps pipeline [project](https://dev.azure.com/guoxiaoq/CommodQuant)

I use FsLab template to document various topics related to quantitative finance with a focus on commodities markets.

Guide in FsLab scripts
-----------------

* [Index](https://xqguo.github.io/guide/)

Programming setup
----------

* Learn FSharp basics.
  * [F# for fun and profit](https://fsharpforfunandprofit.com): a very good site to learn F#.

* Get a free environment
  * [Azure notebook](https://notebooks.azure.com)
  * Install .net core SDK and a IDE or editor
    * VS Community for ease of use and windows environment
    * or Install VSCode for cross-platform
    * Vim + VimSharp plugin for cross-platform use minimal resources
* Contribute to open source project
  * Here is the [documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/?view=azure-devops) to azure devops pipeline  
  * here is the [azure pipeline yaml source code](https://github.com/microsoft/azure-pipelines-yaml/)

Understand the markets
-----------------

* [Intro to Money market](https://docs.google.com/presentation/d/e/2PACX-1vSBtq-1KcZtVHhFnpL0sCLaqKtg5m2FpPKly7bN6X6hPmg5T-Blxo3xD6PTeBFmQt1TJDlJ5x9pZXF0/pub?start=false&loop=false&delayms=3000)

* CME
* ICE
  * [Brent future](https://www.theice.com/products/219/Brent-Crude-Futures) is a benchmark price for global oil.
  * [Brent Futures Option](https://www.theice.com/products/218/Brent-Crude-American-style-Option) is the option on the brent future.  
* LME
* Fed
  * The Fed publishes selected rates, including the fed funds rate [here](https://www.federalreserve.gov/releases/h15/)

Understand the Quantitative finance
------------------

* Notes for [Intro to Derivative Pricing](guide/intro.pdf) and [source](guide/intro.tex)
* Swap price
* Asian option
