#ifdef MS_WIN32
#include <windows.h>
#endif

#if defined(MS_WIN32) || defined(__CYGWIN__)
#define EXPORT(x) __declspec(dllexport) x
#else
#define EXPORT(x) x
#endif

#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <stdio.h>
#include <math.h>
#include <inttypes.h>
#include <wchar.h>


/* some functions handy for testing */

EXPORT(int32_t)
    _testfunc_cbk_reg_int(int32_t a, int32_t b, int32_t c, int32_t d, int32_t e,
        int32_t(*func)(int32_t, int32_t, int32_t, int32_t, int32_t))
{
    return func(a*a, b*b, c*c, d*d, e*e);
}

EXPORT(double)
    _testfunc_cbk_reg_double(double a, double b, double c, double d, double e,
        double(*func)(double, double, double, double, double))
{
    return func(a*a, b*b, c*c, d*d, e*e);
}

/*
* This structure should be the same as in test_callbacks.py and the
* method test_callback_large_struct. See issues 17310 and 20160: the
* structure must be larger than 8 bytes int32_t.
*/

typedef struct {
    uint32_t first;
    uint32_t second;
    uint32_t third;
} Test;

EXPORT(void)
    _testfunc_cbk_large_struct(Test in, void(*func)(Test))
{
    func(in);
}

/*
* See issue 29565. Update a structure passed by value;
* the caller should not see any change.
*/

EXPORT(void)
    _testfunc_large_struct_update_value(Test in)
{
    ((volatile Test *)&in)->first = 0x0badf00d;
    ((volatile Test *)&in)->second = 0x0badf00d;
    ((volatile Test *)&in)->third = 0x0badf00d;
}

typedef struct {
    uint32_t first;
    uint32_t second;
} TestReg;


EXPORT(TestReg) last_tfrsuv_arg = { 0 };


EXPORT(void)
    _testfunc_reg_struct_update_value(TestReg in)
{
    last_tfrsuv_arg = in;
    ((volatile TestReg *)&in)->first = 0x0badf00d;
    ((volatile TestReg *)&in)->second = 0x0badf00d;
}


EXPORT(void)testfunc_array(int32_t values[4])
{
    printf("testfunc_array %d %d %d %d\n",
        values[0],
        values[1],
        values[2],
        values[3]);
}

EXPORT(long double)testfunc_Ddd(double a, double b)
{
    long double result = (long double)(a * b);
    printf("testfunc_Ddd(%p, %p)\n", &a, &b);
    printf("testfunc_Ddd(%g, %g)\n", a, b);
    return result;
}

EXPORT(long double)testfunc_DDD(long double a, long double b)
{
    long double result = a * b;
    printf("testfunc_DDD(%p, %p)\n", &a, &b);
    printf("testfunc_DDD(%Lg, %Lg)\n", a, b);
    return result;
}

EXPORT(int32_t)testfunc_iii(int32_t a, int32_t b)
{
    int32_t result = a * b;
    printf("testfunc_iii(%p, %p)\n", &a, &b);
    return result;
}

EXPORT(int32_t)myprintf(char *fmt, ...)
{
    int32_t result;
    va_list argptr;
    va_start(argptr, fmt);
    result = vprintf(fmt, argptr);
    va_end(argptr);
    return result;
}

EXPORT(char *)my_strtok(char *token, const char *delim)
{
    return strtok(token, delim);
}

EXPORT(char *)my_strchr(const char *s, int32_t c)
{
    return strchr(s, c);
}


EXPORT(double) my_sqrt(double a)
{
    return sqrt(a);
}

EXPORT(void) my_qsort(void *base, size_t num, size_t width, int32_t(*compare)(const void*, const void*))
{
    qsort(base, num, width, compare);
}

EXPORT(int32_t *) _testfunc_ai8(int32_t a[8])
{
    return a;
}

EXPORT(void) _testfunc_v(int32_t a, int32_t b, int32_t *presult)
{
    *presult = a + b;
}

EXPORT(int32_t) _testfunc_i_bhilfd(int8_t b, short h, int32_t i, int32_t l, float f, double d)
{
    /*      printf("_testfunc_i_bhilfd got %d %d %d %ld %f %f\n",
    b, h, i, l, f, d);
    */
    return (int32_t)(b + h + i + l + f + d);
}

EXPORT(float) _testfunc_f_bhilfd(int8_t b, short h, int32_t i, int32_t l, float f, double d)
{
    /*      printf("_testfunc_f_bhilfd got %d %d %d %ld %f %f\n",
    b, h, i, l, f, d);
    */
    return (float)(b + h + i + l + f + d);
}

EXPORT(double) _testfunc_d_bhilfd(int8_t b, short h, int32_t i, int32_t l, float f, double d)
{
    /*      printf("_testfunc_d_bhilfd got %d %d %d %ld %f %f\n",
    b, h, i, l, f, d);
    */
    return (double)(b + h + i + l + f + d);
}

EXPORT(long double) _testfunc_D_bhilfD(int8_t b, short h, int32_t i, int32_t l, float f, long double d)
{
    /*      printf("_testfunc_d_bhilfd got %d %d %d %ld %f %f\n",
    b, h, i, l, f, d);
    */
    return (long double)(b + h + i + l + f + d);
}

EXPORT(char *) _testfunc_p_p(void *s)
{
    return (char *)s;
}

EXPORT(void *) _testfunc_c_p_p(int32_t *argcp, char **argv)
{
    return argv[(*argcp) - 1];
}

EXPORT(void *) get_strchr(void)
{
    return (void *)strchr;
}

EXPORT(char *) my_strdup(char *src)
{
    char *dst = (char *)malloc(strlen(src) + 1);
    if (!dst)
        return NULL;
    strcpy(dst, src);
    return dst;
}

EXPORT(void)my_free(void *ptr)
{
    free(ptr);
}

EXPORT(wchar_t *) my_wcsdup(wchar_t *src)
{
    size_t len = wcslen(src);
    wchar_t *ptr = (wchar_t *)malloc((len + 1) * sizeof(wchar_t));
    if (ptr == NULL)
        return NULL;
    memcpy(ptr, src, (len + 1) * sizeof(wchar_t));
    return ptr;
}

EXPORT(size_t) my_wcslen(wchar_t *src)
{
    return wcslen(src);
}

#ifndef MS_WIN32
# ifndef __stdcall
#  define __stdcall /* */
# endif
#endif

typedef struct {
    int32_t(*c)(int32_t, int32_t);
    int32_t(__stdcall *s)(int32_t, int32_t);
} FUNCS;

EXPORT(int32_t) _testfunc_callfuncp(FUNCS *fp)
{
    fp->c(1, 2);
    fp->s(3, 4);
    return 0;
}

EXPORT(int32_t) _testfunc_deref_pointer(int32_t *pi)
{
    return *pi;
}

#ifdef MS_WIN32
EXPORT(int32_t) _testfunc_piunk(IUnknown FAR *piunk)
{
    piunk->lpVtbl->AddRef(piunk);
    return piunk->lpVtbl->Release(piunk);
}
#endif

EXPORT(int32_t) _testfunc_callback_with_pointer(int32_t(*func)(int32_t *))
{
    int32_t table[] = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    return (*func)(table);
}

EXPORT(int64_t) _testfunc_q_bhilfdq(int8_t b, short h, int32_t i, int32_t l, float f,
    double d, int64_t q)
{
    return (int64_t)(b + h + i + l + f + d + q);
}

EXPORT(int64_t) _testfunc_q_bhilfd(int8_t b, short h, int32_t i, int32_t l, float f, double d)
{
    return (int64_t)(b + h + i + l + f + d);
}

EXPORT(int32_t) _testfunc_callback_i_if(int32_t value, int32_t(*func)(int32_t))
{
    int32_t sum = 0;
    while (value != 0) {
        sum += func(value);
        value /= 2;
    }
    return sum;
}

EXPORT(int64_t) _testfunc_callback_q_qf(int64_t value,
    int64_t(*func)(int64_t))
{
    int64_t sum = 0;

    while (value != 0) {
        sum += func(value);
        value /= 2;
    }
    return sum;
}

typedef struct {
    char *name;
    char *value;
} SPAM;

typedef struct {
    char *name;
    int32_t num_spams;
    SPAM *spams;
} EGG;

SPAM my_spams[2] = {
    { "name1", "value1" },
{ "name2", "value2" },
};

EGG my_eggs[1] = {
    { "first egg", 1, my_spams }
};

EXPORT(int32_t) getSPAMANDEGGS(EGG **eggs)
{
    *eggs = my_eggs;
    return 1;
}

typedef struct tagpoint {
    int32_t x;
    int32_t y;
} point;

EXPORT(int32_t) _testfunc_byval(point in, point *pout)
{
    if (pout) {
        pout->x = in.x;
        pout->y = in.y;
    }
    return in.x + in.y;
}

EXPORT(int32_t) an_integer = 42;

EXPORT(int32_t) get_an_integer(void)
{
    return an_integer;
}

EXPORT(double)
    integrate(double a, double b, double(*f)(double), int32_t nstep)
{
    double x, sum = 0.0, dx = (b - a) / (double)nstep;
    for (x = a + 0.5*dx; (b - x)*(x - a) > 0.0; x += dx)
        sum += f(x);
    return sum / (double)nstep;
}

typedef struct {
    void(*initialize)(void *(*)(int32_t), void(*)(void *));
} xxx_library;

static void _xxx_init(void *(*Xalloc)(int32_t), void(*Xfree)(void *))
{
    void *ptr;

    printf("_xxx_init got %p %p\n", Xalloc, Xfree);
    printf("calling\n");
    ptr = Xalloc(32);
    Xfree(ptr);
    printf("calls done, ptr was %p\n", ptr);
}

xxx_library _xxx_lib = {
    _xxx_init
};

EXPORT(xxx_library) *library_get(void)
{
    return &_xxx_lib;
}

#ifdef MS_WIN32
/* See Don Box (german), pp 79ff. */
EXPORT(void) GetString(BSTR *pbstr)
{
    *pbstr = SysAllocString(L"Goodbye!");
}
#endif

#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wunused-parameter"
EXPORT(void) _py_func_si(char *s, int32_t i)
{
}
#pragma GCC diagnostic pop

EXPORT(void) _py_func(void)
{
}

EXPORT(int64_t) last_tf_arg_s = 0;
EXPORT(uint64_t) last_tf_arg_u = 0;

struct BITS {
    int32_t A : 1, B : 2, C : 3, D : 4, E : 5, F : 6, G : 7, H : 8, I : 9;
    short M : 1, N : 2, O : 3, P : 4, Q : 5, R : 6, S : 7;
};

EXPORT(void) set_bitfields(struct BITS *bits, char name, int32_t value)
{
    switch (name) {
    case 'A': bits->A = value; break;
    case 'B': bits->B = value; break;
    case 'C': bits->C = value; break;
    case 'D': bits->D = value; break;
    case 'E': bits->E = value; break;
    case 'F': bits->F = value; break;
    case 'G': bits->G = value; break;
    case 'H': bits->H = value; break;
    case 'I': bits->I = value; break;

    case 'M': bits->M = value; break;
    case 'N': bits->N = value; break;
    case 'O': bits->O = value; break;
    case 'P': bits->P = value; break;
    case 'Q': bits->Q = value; break;
    case 'R': bits->R = value; break;
    case 'S': bits->S = value; break;
    }
}

EXPORT(int32_t) unpack_bitfields(struct BITS *bits, char name)
{
    switch (name) {
    case 'A': return bits->A;
    case 'B': return bits->B;
    case 'C': return bits->C;
    case 'D': return bits->D;
    case 'E': return bits->E;
    case 'F': return bits->F;
    case 'G': return bits->G;
    case 'H': return bits->H;
    case 'I': return bits->I;

    case 'M': return bits->M;
    case 'N': return bits->N;
    case 'O': return bits->O;
    case 'P': return bits->P;
    case 'Q': return bits->Q;
    case 'R': return bits->R;
    case 'S': return bits->S;
    }
    return 0;
}

#define S last_tf_arg_s = (int64_t)c
#define U last_tf_arg_u = (uint64_t)c

EXPORT(int8_t) tf_b(int8_t c) { S; return c / 3; }
EXPORT(uint8_t) tf_B(uint8_t c) { U; return c / 3; }
EXPORT(short) tf_h(short c) { S; return c / 3; }
EXPORT(uint16_t) tf_H(uint16_t c) { U; return c / 3; }
EXPORT(int32_t) tf_i(int32_t c) { S; return c / 3; }
EXPORT(uint32_t) tf_I(uint32_t c) { U; return c / 3; }
EXPORT(int32_t) tf_l(int32_t c) { S; return c / 3; }
EXPORT(uint32_t) tf_L(uint32_t c) { U; return c / 3; }
EXPORT(int64_t) tf_q(int64_t c) { S; return c / 3; }
EXPORT(uint64_t) tf_Q(uint64_t c) { U; return c / 3; }
EXPORT(float) tf_f(float c) { S; return c / 3; }
EXPORT(double) tf_d(double c) { S; return c / 3; }
EXPORT(long double) tf_D(long double c) { S; return c / 3; }

#ifdef MS_WIN32
EXPORT(int8_t) __stdcall s_tf_b(int8_t c) { S; return c / 3; }
EXPORT(uint8_t) __stdcall s_tf_B(uint8_t c) { U; return c / 3; }
EXPORT(short) __stdcall s_tf_h(short c) { S; return c / 3; }
EXPORT(uint16_t) __stdcall s_tf_H(uint16_t c) { U; return c / 3; }
EXPORT(int32_t) __stdcall s_tf_i(int32_t c) { S; return c / 3; }
EXPORT(uint32_t) __stdcall s_tf_I(uint32_t c) { U; return c / 3; }
EXPORT(int32_t) __stdcall s_tf_l(int32_t c) { S; return c / 3; }
EXPORT(uint32_t) __stdcall s_tf_L(uint32_t c) { U; return c / 3; }
EXPORT(int64_t) __stdcall s_tf_q(int64_t c) { S; return c / 3; }
EXPORT(uint64_t) __stdcall s_tf_Q(uint64_t c) { U; return c / 3; }
EXPORT(float) __stdcall s_tf_f(float c) { S; return c / 3; }
EXPORT(double) __stdcall s_tf_d(double c) { S; return c / 3; }
EXPORT(long double) __stdcall s_tf_D(long double c) { S; return c / 3; }
#endif
/*******/

#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wunused-parameter"
EXPORT(int8_t) tf_bb(int8_t x, int8_t c) { S; return c / 3; }
EXPORT(uint8_t) tf_bB(int8_t x, uint8_t c) { U; return c / 3; }
EXPORT(short) tf_bh(int8_t x, short c) { S; return c / 3; }
EXPORT(uint16_t) tf_bH(int8_t x, uint16_t c) { U; return c / 3; }
EXPORT(int32_t) tf_bi(int8_t x, int32_t c) { S; return c / 3; }
EXPORT(uint32_t) tf_bI(int8_t x, uint32_t c) { U; return c / 3; }
EXPORT(int32_t) tf_bl(int8_t x, int32_t c) { S; return c / 3; }
EXPORT(uint32_t) tf_bL(int8_t x, uint32_t c) { U; return c / 3; }
EXPORT(int64_t) tf_bq(int8_t x, int64_t c) { S; return c / 3; }
EXPORT(uint64_t) tf_bQ(int8_t x, uint64_t c) { U; return c / 3; }
EXPORT(float) tf_bf(int8_t x, float c) { S; return c / 3; }
EXPORT(double) tf_bd(int8_t x, double c) { S; return c / 3; }
EXPORT(long double) tf_bD(int8_t x, long double c) { S; return c / 3; }
EXPORT(void) tv_i(int32_t c) { S; return; }
#pragma GCC diagnostic pop

#ifdef MS_WIN32
EXPORT(int8_t) __stdcall s_tf_bb(int8_t x, int8_t c) { S; return c / 3; }
EXPORT(uint8_t) __stdcall s_tf_bB(int8_t x, uint8_t c) { U; return c / 3; }
EXPORT(short) __stdcall s_tf_bh(int8_t x, short c) { S; return c / 3; }
EXPORT(uint16_t) __stdcall s_tf_bH(int8_t x, uint16_t c) { U; return c / 3; }
EXPORT(int32_t) __stdcall s_tf_bi(int8_t x, int32_t c) { S; return c / 3; }
EXPORT(uint32_t) __stdcall s_tf_bI(int8_t x, uint32_t c) { U; return c / 3; }
EXPORT(int32_t) __stdcall s_tf_bl(int8_t x, int32_t c) { S; return c / 3; }
EXPORT(uint32_t) __stdcall s_tf_bL(int8_t x, uint32_t c) { U; return c / 3; }
EXPORT(int64_t) __stdcall s_tf_bq(int8_t x, int64_t c) { S; return c / 3; }
EXPORT(uint64_t) __stdcall s_tf_bQ(int8_t x, uint64_t c) { U; return c / 3; }
EXPORT(float) __stdcall s_tf_bf(int8_t x, float c) { S; return c / 3; }
EXPORT(double) __stdcall s_tf_bd(int8_t x, double c) { S; return c / 3; }
EXPORT(long double) __stdcall s_tf_bD(int8_t x, long double c) { S; return c / 3; }
EXPORT(void) __stdcall s_tv_i(int32_t c) { S; return; }
#endif

/********/

#ifndef MS_WIN32

typedef struct {
    int32_t x;
    int32_t y;
} POINT;

typedef struct {
    int32_t left;
    int32_t top;
    int32_t right;
    int32_t bottom;
} RECT;

#endif

EXPORT(int32_t) PointInRect(RECT *prc, POINT pt)
{
    if (pt.x < prc->left)
        return 0;
    if (pt.x > prc->right)
        return 0;
    if (pt.y < prc->top)
        return 0;
    if (pt.y > prc->bottom)
        return 0;
    return 1;
}

EXPORT(int32_t left = 10);
EXPORT(int32_t top = 20);
EXPORT(int32_t right = 30);
EXPORT(int32_t bottom = 40);

EXPORT(RECT) ReturnRect(int32_t i, RECT ar, RECT* br, POINT cp, RECT dr,
    RECT *er, POINT fp, RECT gr)
{
    /*Check input */
    if (ar.left + br->left + dr.left + er->left + gr.left != left * 5)
    {
        ar.left = 100;
        return ar;
    }
    if (ar.right + br->right + dr.right + er->right + gr.right != right * 5)
    {
        ar.right = 100;
        return ar;
    }
    if (cp.x != fp.x)
    {
        ar.left = -100;
    }
    if (cp.y != fp.y)
    {
        ar.left = -200;
    }
    switch (i)
    {
    case 0:
        return ar;
        break;
    case 1:
        return dr;
        break;
    case 2:
        return gr;
        break;

    }
    return ar;
}

typedef struct {
    short x;
    short y;
} S2H;

EXPORT(S2H) ret_2h_func(S2H inp)
{
    inp.x *= 2;
    inp.y *= 3;
    return inp;
}

typedef struct {
    int32_t a, b, c, d, e, f, g, h;
} S8I;

EXPORT(S8I) ret_8i_func(S8I inp)
{
    inp.a *= 2;
    inp.b *= 3;
    inp.c *= 4;
    inp.d *= 5;
    inp.e *= 6;
    inp.f *= 7;
    inp.g *= 8;
    inp.h *= 9;
    return inp;
}

EXPORT(int32_t) GetRectangle(int32_t flag, RECT *prect)
{
    if (flag == 0)
        return 0;
    prect->left = (int32_t)flag;
    prect->top = (int32_t)flag + 1;
    prect->right = (int32_t)flag + 2;
    prect->bottom = (int32_t)flag + 3;
    return 1;
}

EXPORT(void) TwoOutArgs(int32_t a, int32_t *pi, int32_t b, int32_t *pj)
{
    *pi += a;
    *pj += b;
}

#ifdef MS_WIN32
EXPORT(S2H) __stdcall s_ret_2h_func(S2H inp) { return ret_2h_func(inp); }
EXPORT(S8I) __stdcall s_ret_8i_func(S8I inp) { return ret_8i_func(inp); }
#endif

#ifdef MS_WIN32
/* Should port this */
#include <stdlib.h>
#include <search.h>

EXPORT(HRESULT) KeepObject(IUnknown *punk)
{
    static IUnknown *pobj;
    if (punk)
        punk->lpVtbl->AddRef(punk);
    if (pobj)
        pobj->lpVtbl->Release(pobj);
    pobj = punk;
    return S_OK;
}

#endif
