#!/usr/bin/env python3
"""Gera modelos 3D low-poly (OBJ + MTL) para o jogo, sem dependências externas.
Saída: client/Assets/Resources/Models/<Pasta>/<nome>.obj. Unidades em metros, Y para cima, base em y=0.
Uso: python3 tools/generate_models.py   (idempotente: sobrescreve os arquivos gerados)"""
import math, os, random

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "client", "Assets", "Resources", "Models")

def hexc(h): return tuple(int(h[i:i+2], 16) / 255 for i in (1, 3, 5))
MATS = {
    "casca": "#5C4030", "casca_escura": "#3F2C20", "folha": "#2F6B2E", "folha_clara": "#4C8F38", "folha_escura": "#22521F",
    "pinheiro": "#1F5A38", "pinheiro_escuro": "#164630", "pedra": "#8A8A90", "pedra_clara": "#A6A6AC", "pedra_escura": "#5E5E66",
    "musgo": "#527F3C", "madeira": "#7A5230", "madeira_escura": "#4A3020", "reboco": "#D8CBB0", "telhado": "#8E3B2C",
    "telhado_escuro": "#6E2B20", "pano": "#B8A98A", "pano_azul": "#3F5F9A", "brasa": "#E0521A", "chama": "#FFB030",
    "chama_clara": "#FFE680", "runa": "#D93A28", "janela": "#2C3A55", "terra": "#4A3A2E", "agua": "#2F5F8F",
}

class Mesh:
    def __init__(self): self.tris = []  # (material, a, b, c)
    def part(self, mat, tris):
        vol = sum(dot(a, cross(b, c)) for a, b, c in tris) / 6
        if vol < 0: tris = [(a, c, b) for a, b, c in tris]
        self.tris += [(mat, a, b, c) for a, b, c in tris]

def sub(a, b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def dot(a, b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]
def norm(a):
    l = math.sqrt(dot(a, a)) or 1
    return (a[0]/l, a[1]/l, a[2]/l)
def rot(p, yaw, cx, cz):
    c, s = math.cos(yaw), math.sin(yaw); x, z = p[0]-cx, p[2]-cz
    return (cx + x*c + z*s, p[1], cz - x*s + z*c)

def quads(q): 
    t = []
    for a, b, c, d in q: t += [(a, b, c), (a, c, d)]
    return t

def box(m, mat, cx, y0, cz, sx, sy, sz, yaw=0.0):
    hx, hz = sx/2, sz/2
    p = lambda x, y, z: rot((cx+x, y0+y, cz+z), yaw, cx, cz)
    v = [p(-hx,0,-hz), p(hx,0,-hz), p(hx,0,hz), p(-hx,0,hz), p(-hx,sy,-hz), p(hx,sy,-hz), p(hx,sy,hz), p(-hx,sy,hz)]
    m.part(mat, quads([(v[0],v[1],v[2],v[3]), (v[4],v[7],v[6],v[5]), (v[0],v[4],v[5],v[1]), (v[1],v[5],v[6],v[2]), (v[2],v[6],v[7],v[3]), (v[3],v[7],v[4],v[0])]))

def frustum(m, mat, cx, y0, cz, r0, r1, h, n=8, caps=True):
    b = [(cx+r0*math.cos(2*math.pi*i/n), y0, cz+r0*math.sin(2*math.pi*i/n)) for i in range(n)]
    t = [(cx+r1*math.cos(2*math.pi*i/n), y0+h, cz+r1*math.sin(2*math.pi*i/n)) for i in range(n)]
    tr = []
    for i in range(n):
        j = (i+1) % n
        if r1 < 1e-6: tr.append((b[i], b[j], (cx, y0+h, cz)))
        else: tr += quads([(b[i], b[j], t[j], t[i])])
    if caps:
        tr += [((cx, y0, cz), b[j], b[i]) for i in range(n) for j in [(i+1) % n]]
        if r1 >= 1e-6: tr += [((cx, y0+h, cz), t[i], t[(i+1) % n]) for i in range(n)]
    m.part(mat, tr)

def gable(m, mat, cx, y0, cz, L, W, H, yaw=0.0):
    p = lambda x, y, z: rot((cx+x, y0+y, cz+z), yaw, cx, cz)
    a, b, c, d = p(-L/2,0,-W/2), p(L/2,0,-W/2), p(L/2,0,W/2), p(-L/2,0,W/2)
    r1, r2 = p(-L/2,H,0), p(L/2,H,0)
    m.part(mat, quads([(a,b,c,d), (a,r1,r2,b), (d,c,r2,r1)]) + [(a,d,r1), (b,r2,c)])

_ICO = None
def icosphere():
    global _ICO
    if _ICO: return _ICO
    t = (1+5**0.5)/2
    v = [norm(p) for p in [(-1,t,0),(1,t,0),(-1,-t,0),(1,-t,0),(0,-1,t),(0,1,t),(0,-1,-t),(0,1,-t),(t,0,-1),(t,0,1),(-t,0,-1),(-t,0,1)]]
    f = [(0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),(1,5,9),(5,11,4),(11,10,2),(10,7,6),(7,1,8),(3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),(4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1)]
    _ICO = (v, f); return _ICO

def blob(m, mat, cx, cy, cz, rx, ry, rz, seed=0, jit=0.12):
    rnd = random.Random(seed); v, f = icosphere()
    pts = []
    for p in v:
        k = 1 + rnd.uniform(-jit, jit)
        pts.append((cx+p[0]*rx*k, cy+p[1]*ry*k, cz+p[2]*rz*k))
    m.part(mat, [(pts[a], pts[b], pts[c]) for a, b, c in f])

def write(folder, name, m):
    d = os.path.join(ROOT, folder); os.makedirs(d, exist_ok=True)
    used = sorted({t[0] for t in m.tris})
    with open(os.path.join(d, name + ".mtl"), "w") as f:
        for u in used:
            r, g, b = hexc(MATS[u]); f.write("newmtl %s\nKa %.3f %.3f %.3f\nKd %.3f %.3f %.3f\nKs 0 0 0\nd 1\nillum 1\n\n" % (u, r, g, b, r, g, b))
    n = 0
    with open(os.path.join(d, name + ".obj"), "w") as f:
        f.write("# gerado por tools/generate_models.py\nmtllib %s.mtl\no %s\n" % (name, name))
        for u in used:
            f.write("usemtl %s\n" % u)
            for mt, a, b, c in m.tris:
                if mt != u: continue
                nr = norm(cross(sub(b, a), sub(c, a)))
                for p in (a, b, c): f.write("v %.4f %.4f %.4f\n" % p)
                f.write("vn %.4f %.4f %.4f\nf %d//%d %d//%d %d//%d\n" % ((nr[0], nr[1], nr[2]) + (n+1, n//3+1, n+2, n//3+1, n+3, n//3+1)))
                n += 3
    ys = [p[1] for t in m.tris for p in t[1:]]
    xs = [p[0] for t in m.tris for p in t[1:]]; zs = [p[2] for t in m.tris for p in t[1:]]
    print("%-10s %-10s tris=%4d  alt=%.1f  larg=%.1f x %.1f  y_min=%.2f" % (folder, name, len(m.tris), max(ys), max(xs)-min(xs), max(zs)-min(zs), min(ys)))

# ------------------------------------------------------------------ vegetação e pedras
def tree_broadleaf():
    m = Mesh()
    frustum(m, "casca", 0, 0, 0, 0.36, 0.2, 3.4, 7)
    blob(m, "folha", 0, 4.7, 0, 2.1, 1.7, 2.1, 1)
    blob(m, "folha_clara", 1.2, 4.0, 0.5, 1.4, 1.2, 1.4, 2)
    blob(m, "folha_escura", -1.1, 4.2, -0.5, 1.5, 1.2, 1.5, 3)
    blob(m, "folha_clara", 0.2, 5.8, -0.2, 1.3, 1.0, 1.3, 4)
    return m
def tree_oak():
    m = Mesh()
    frustum(m, "casca_escura", 0, 0, 0, 0.62, 0.3, 3.2, 8)
    frustum(m, "casca_escura", 0, -0.05, 0, 0.8, 0.62, 0.5, 8)
    for i, (x, y, z, r, mt) in enumerate([(0, 5.2, 0, 2.6, "folha"), (2.0, 4.5, 0.6, 1.8, "folha_escura"), (-2.0, 4.6, -0.4, 1.9, "folha"),
                                         (0.5, 4.4, -2.0, 1.7, "folha_clara"), (-0.4, 4.5, 2.0, 1.8, "folha_escura"), (0.3, 6.4, 0.2, 1.7, "folha_clara")]):
        blob(m, mt, x, y, z, r, r*0.8, r, 10+i)
    return m
def tree_pine():
    m = Mesh()
    frustum(m, "casca", 0, 0, 0, 0.3, 0.18, 2.4, 6)
    for i, (y, r, h) in enumerate([(1.5, 2.3, 2.3), (2.8, 1.9, 2.1), (4.0, 1.5, 2.0), (5.1, 1.1, 1.8), (6.1, 0.7, 1.6)]):
        frustum(m, "pinheiro" if i % 2 == 0 else "pinheiro_escuro", 0, y, 0, r, 0.0, h, 7)
    return m
def rock():
    m = Mesh()
    blob(m, "pedra", 0, 0.5, 0, 1.0, 0.62, 0.9, 21, 0.16)
    blob(m, "pedra_escura", 0.85, 0.28, 0.3, 0.55, 0.38, 0.5, 22, 0.14)
    blob(m, "pedra_clara", -0.6, 0.25, -0.65, 0.42, 0.3, 0.4, 23, 0.14)
    blob(m, "musgo", -0.15, 0.95, 0.05, 0.55, 0.14, 0.5, 24, 0.1)
    return m

# ------------------------------------------------------------------ ruína, acampamento, arena
def ruin():
    m = Mesh()
    frustum(m, "pedra_escura", 0, 0, 0, 2.8, 2.6, 0.18, 12)
    box(m, "pedra", -1.4, 0.18, -0.2, 2.4, 1.1, 0.55, 0.08)
    box(m, "pedra_clara", 1.3, 0.18, -1.0, 0.55, 1.8, 1.6, -0.1)
    box(m, "pedra", 0.6, 0.18, 1.7, 1.6, 0.6, 0.5, 0.25)
    frustum(m, "pedra_clara", -1.9, 0.18, 0.4, 0.38, 0.34, 3.4, 8)
    box(m, "pedra", -1.9, 3.58, 0.4, 0.95, 0.3, 0.95, 0.4)
    frustum(m, "pedra_clara", 1.1, 0.18, 1.0, 0.38, 0.34, 1.9, 8)
    box(m, "pedra", 1.35, 2.08, 1.15, 0.5, 0.25, 0.45, 0.8)
    box(m, "pedra_escura", 0.2, 0.18, 0.2, 0.9, 0.4, 0.7, 0.5)
    box(m, "pedra", -0.8, 0.18, 1.2, 0.5, 0.35, 0.5, 1.0)
    blob(m, "musgo", -1.4, 1.25, -0.2, 1.0, 0.12, 0.35, 31, 0.1)
    blob(m, "musgo", -1.9, 2.2, 0.75, 0.12, 0.9, 0.2, 32, 0.1)
    return m
def camp():
    m = Mesh()
    for i in range(8):
        a = i*math.pi/4
        blob(m, "pedra" if i % 2 else "pedra_clara", 3.2*math.cos(a), 0.2, 3.2*math.sin(a), 0.42, 0.3, 0.4, 40+i, 0.15)
    frustum(m, "terra", 0, 0, 0, 1.0, 0.9, 0.08, 10)
    for k in range(3): box(m, "madeira", 0, 0.08, 0, 1.5, 0.16, 0.16, k*math.pi/3)
    frustum(m, "brasa", 0, 0.1, 0, 0.5, 0.0, 1.1, 7)
    frustum(m, "chama", 0, 0.1, 0, 0.32, 0.0, 1.4, 7)
    frustum(m, "chama_clara", 0, 0.1, 0, 0.16, 0.0, 0.9, 6)
    for (x, z, yaw, mt) in [(-6.2, -2.5, 0.5, "pano"), (-5.0, 3.6, -0.4, "pano_azul")]:
        gable(m, mt, x, 0.0, z, 2.8, 2.4, 1.9, yaw)
        box(m, "madeira_escura", x, 0, z, 2.9, 0.08, 2.5, yaw)
    for (x, z, yaw) in [(2.2, 3.6, 0.3), (-2.8, 2.6, 1.9), (3.0, -2.2, -0.5)]: box(m, "madeira", x, 0, z, 1.3, 0.38, 0.4, yaw)
    frustum(m, "pedra_clara", 4.5, 0, 0, 0.4, 0.34, 2.4, 8)
    box(m, "pedra", 4.5, 2.4, 0, 0.85, 0.2, 0.85)
    frustum(m, "chama", 4.5, 2.6, 0, 0.26, 0.0, 0.7, 6)
    return m
def arena():
    m = Mesh()
    for i in range(8):
        a = i*math.pi/4 + 0.2; x, z = 10*math.cos(a), 10*math.sin(a)
        box(m, "pedra_escura", x, 0, z, 1.3, 4.0 + 0.7*(i % 3), 1.0, -a + math.pi/2)
        box(m, "runa", 9.35*math.cos(a), 2.6, 9.35*math.sin(a), 0.25, 0.8, 0.1, -a + math.pi/2)
        box(m, "pedra", x, 4.0 + 0.7*(i % 3), z, 1.5, 0.25, 1.2, -a + math.pi/2)
    for i in range(28):
        a = i*2*math.pi/28
        box(m, "runa", 8.6*math.cos(a), 0.02, 8.6*math.sin(a), 1.4, 0.05, 0.25, -a + math.pi/2)
    return m

# ------------------------------------------------------------------ aldeia
def house():
    m = Mesh()
    box(m, "pedra", 0, 0, 0, 3.8, 0.5, 3.2)
    box(m, "reboco", 0, 0.5, 0, 3.6, 2.0, 3.0)
    for sx in (-1, 1):
        for sz in (-1, 1): box(m, "madeira_escura", sx*1.8, 0.5, sz*1.5, 0.22, 2.0, 0.22)
    box(m, "madeira_escura", 0, 2.38, 0, 3.7, 0.16, 3.1)
    gable(m, "telhado", 0, 2.5, 0, 4.4, 3.9, 1.7)
    gable(m, "telhado_escuro", 0, 2.45, 0, 4.5, 3.95, 0.1)
    box(m, "madeira", 0, 0.5, 1.52, 0.95, 1.7, 0.1)
    box(m, "janela", 1.0, 1.4, 1.52, 0.6, 0.6, 0.06); box(m, "janela", -1.2, 1.4, 1.52, 0.6, 0.6, 0.06)
    box(m, "janela", 1.82, 1.4, 0, 0.06, 0.6, 0.6)
    box(m, "pedra_clara", -1.2, 2.5, -0.6, 0.55, 2.0, 0.55)
    return m
def tower():
    m = Mesh()
    frustum(m, "pedra_escura", 0, 0, 0, 2.2, 2.0, 0.6, 10)
    frustum(m, "pedra", 0, 0.6, 0, 1.9, 1.7, 7.0, 10)
    frustum(m, "pedra_clara", 0, 7.6, 0, 2.15, 2.15, 0.35, 10)
    for i in range(8):
        a = i*math.pi/4; box(m, "pedra_clara", 2.0*math.cos(a), 7.95, 2.0*math.sin(a), 0.6, 0.5, 0.6, -a)
    frustum(m, "telhado", 0, 8.45, 0, 1.9, 0.0, 3.0, 10)
    for h in (2.5, 4.8): box(m, "janela", 0, h, 1.75, 0.5, 0.9, 0.1)
    box(m, "madeira", 0, 0.6, 1.85, 0.95, 1.6, 0.12)
    return m
def well():
    m = Mesh()
    frustum(m, "pedra", 0, 0, 0, 1.15, 1.05, 0.95, 10)
    frustum(m, "agua", 0, 0.82, 0, 0.85, 0.85, 0.05, 10)
    for sx in (-1, 1): box(m, "madeira", sx*1.0, 0.9, 0, 0.16, 1.7, 0.16)
    box(m, "madeira_escura", 0, 2.2, 0, 2.3, 0.14, 0.14)
    gable(m, "telhado", 0, 2.55, 0, 2.2, 1.7, 0.7, math.pi/2)
    box(m, "madeira", 0, 1.6, 0, 0.3, 0.3, 0.3)
    return m

OUT = [("Props", "tree_0", tree_broadleaf), ("Props", "tree_1", tree_oak), ("Props", "tree_2", tree_pine), ("Props", "rock", rock),
       ("Buildings", "ruin", ruin), ("Buildings", "camp", camp), ("Buildings", "arena", arena),
       ("Buildings", "house", house), ("Buildings", "tower", tower), ("Buildings", "well", well)]
if __name__ == "__main__":
    for folder, name, fn in OUT: write(folder, name, fn())
