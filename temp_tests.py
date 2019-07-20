import traceback
import sys

def test1():
    try:
        raise Exception
    except:
        pass
    assert sys.exc_info() == (None, None, None)

test1()

def test2():
    def gen():
        try:
            raise Exception
        except:
            exc_info = sys.exc_info()
            yield exc_info
            yield sys.exc_info()
        yield sys.exc_info()
    x = gen()
    exc_info = next(x)
    assert sys.exc_info() == (None, None, None)
    assert next(x) == exc_info
    assert next(x) == (None, None, None)

test2()

def test3():
    try:
        try:
            raise Exception(1)
        except:
            pass    
        raise Exception(2)
    except Exception as e:
        assert e.__context__ is None

test3()

def test4():
    def gen():
        try:
            raise Exception
        except:
            pass
        yield sys.exc_info()
    x = gen()
    try:
        raise Exception
    except:
        exc_info = sys.exc_info()
        assert next(x) == exc_info

test4()

def test5():
    def gen():
        yield sys.exc_info()
        try:
            raise Exception
        except:
            pass
        yield sys.exc_info()
    x = gen()
    assert next(x) == (None, None, None)
    try:
        raise Exception
    except:
        exc_info = sys.exc_info()
        assert next(x) == exc_info

test5()

def test6():
    def gen():
        yield sys.exc_info()
        try:
            raise Exception
        except:
            pass
        yield sys.exc_info()
    x = gen()
    try:
        raise Exception
    except:
        exc_info_1 = sys.exc_info()
        assert next(x) == exc_info_1
    try:
        raise Exception
    except:
        exc_info_2 = sys.exc_info()
        assert next(x) == exc_info_2
    assert exc_info_1 != exc_info_2

test6()

def test7():
    def gen():
        try:
            raise Exception
        except:
            yield traceback.format_exc()
            yield traceback.format_exc()
        
    x = gen()
    try:
        raise Exception('@Exception 1')
    except:
        assert '@Exception 1' in next(x)
    try:
        raise Exception('@Exception 2')
    except:
        assert '@Exception 1' in next(x)

test7()

def test8():
    def gen():
        try:
            raise Exception
        except:
            yield traceback.format_exc()
        try:
            raise Exception
        except:
            yield traceback.format_exc()
        
    x = gen()
    try:
        raise Exception('@Exception 1')
    except:
        assert '@Exception 1' in next(x)
    try:
        raise Exception('@Exception 2')
    except:
        res = next(x)
        assert '@Exception 2' in res
        assert '@Exception 1' not in res

test8()

def test9():
    def gen():
        try:
            raise Exception
        except:
            yield traceback.format_exc()
        yield "Hello"
        try:
            raise Exception
        except:
            yield traceback.format_exc()
        
    x = gen()
    try:
        raise Exception('@Exception 1')
    except:
        assert '@Exception 1' in next(x)
    next(x)
    try:
        raise Exception('@Exception 2')
    except:
        res = next(x)
        assert '@Exception 2' in res
        assert '@Exception 1' not in res

test9()

def test10():
    def gen():
        yield 1
        yield 2
        yield 3
    x = list(gen())
    assert x == [1,2,3]
    assert sys.exc_info() == (None, None, None)

test10()

def test11():
    def gen1():
        yield 1
        yield 2
        yield 3
    def gen2():
        yield 4
        yield from gen1()
        yield 5
    x = list(gen2())
    assert x == [4,1,2,3,5]
    assert sys.exc_info() == (None, None, None)

test11()

def test12():
    try:
        try:
            raise Exception('1')
        except:
            ex = Exception('2')
            raise ex
        finally:
            assert sys.exc_info()[1] == ex
    except:
        assert sys.exc_info()[1] == ex

test12()

def test13():
    try:
        try:
            ex = Exception()
            raise ex
        finally:
            print(sys.exc_info())
            assert sys.exc_info()[1] == ex
    except:
        pass

test13()

test1()
test2()
test3()
test4()
test5()
test6()
test7()
test8()
test9()
test10()
test11()
test12()
test13()
