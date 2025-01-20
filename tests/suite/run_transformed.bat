@echo OFF
setlocal EnableDelayedExpansion

IF EXIST Failed_Testcases (
    RMDIR /s /q Failed_Testcases
)
MKDIR Failed_Testcases   

IF "%1"=="try_ipy" (
    goto Transform
)
IF "%1"=="try_one" (
    goto Transform
)

IF EXIST CPy_Testcases (
    RMDIR /s /q CPy_Testcases
)
MKDIR CPy_Testcases   

echo Copying CPython testcases...

FOR /R %Dlr_Root%\Test\Wrappers\Languages\IronPython\cpy %%G IN (test*_cpy.GenericTest) DO (       
    set FILENAME=%%~nG
    set FILENAME=!FILENAME:_cpy=.py!
    FINDSTR /L /I /M /C:"due_to_ironpython" %DLR_ROOT%\External.LCA_RESTRICTED\Languages\IronPython\27\lib\test\!FILENAME! >> CPy_Testcases\file_list.txt 
    IF !errorlevel!==1 (
        copy %DLR_ROOT%\External.LCA_RESTRICTED\Languages\IronPython\27\lib\test\!FILENAME! CPy_Testcases\!FILENAME!
    )  
)	

:Transform
@echo ON
@%DLR_ROOT%\Bin\Debug\ipy transform.py %1 %2 %3
@echo OFF

IF EXIST Transformed\Output.txt DEL Transformed\Output.txt

FOR /R Transformed %%G IN (g_test*.py) DO (
    echo Running %%~nG.py...
	%DLR_ROOT%\Bin\Debug\ipy %%G >> Transformed\Output.txt 2>&1
	IF !ERRORLEVEL!==0 (echo Pass)
	IF NOT !ERRORLEVEL!==0 (
        echo Fail 
        COPY %%G .\Failed_Testcases\

    )
)	
echo Run finished.
EndLocal