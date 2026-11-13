# gsim with the tyre exactly as LandingGear.FixedUpdate (IL): lateral = clamp(clamp(slip,+-10) x response x (0.2+0.01|ws|), +-1) muN
# perpendicular to the contact velocity (opposing the slide), scrub = |sin slip| muN and brake = brake x muN along -velocity,
# total ClampMagnitude(muN). (gsim.py put the lateral force perpendicular to the wheel and the drag along the wheel.)
import math
import gsim
D2R, R2D = math.pi/180, 180/math.pi
def tyre_il(vloc, heading_deg, N, mu, brake=0.0):
    sp = math.hypot(vloc[0], vloc[1])
    if sp < 0.05: return 0.0, 0.0, 0.0
    vx, vy = vloc[0]/sp, vloc[1]/sp
    vel_ang = math.atan2(vloc[1], vloc[0])*R2D
    slip = vel_ang - heading_deg                       # + = velocity right of the wheel
    slipc = max(-10.0, min(10.0, slip))
    lat = max(-1.0, min(1.0, slipc*(0.2 + 0.01*sp)))*mu*N   # magnitude along 'left of velocity' when slip > 0
    lx, ly = vy, -vx                                   # left of velocity in body (x fwd, y right): rotate v by -90
    scrub = abs(math.sin(slip*D2R))*mu*N
    fx = lat*lx - (scrub + brake*mu*N)*vx
    fy = lat*ly - (scrub + brake*mu*N)*vy
    m = math.hypot(fx, fy)
    if m > mu*N: fx *= mu*N/m; fy *= mu*N/m
    return fx, fy, slip
gsim.tyre = tyre_il
