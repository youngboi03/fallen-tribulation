-- Pixel-clean one PixelLab sprite in headless Aseprite (run through tools/ase_clean.sh).
-- Fixes: alpha to 0/255; rare colours within `dist` of a common colour merge into it (ramps of common colours stay).
-- Reports only: isolated pixels (no opaque 8-neighbour), colours used by <=3 pixels, 2x-upscaled look.
-- Params: src, dist (8), rare (0.015 of opaque pixels, at least 3 px), apply (1 = overwrite src), preview (png path), selftest.
local pc = app.pixelColor

local function opaque(v) return pc.rgba(pc.rgbaR(v), pc.rgbaG(v), pc.rgbaB(v), 255) end
local function d2(a, b)
  local r, g, bb = pc.rgbaR(a) - pc.rgbaR(b), pc.rgbaG(a) - pc.rgbaG(b), pc.rgbaB(a) - pc.rgbaB(b)
  return r * r + g * g + bb * bb
end

local function clean(spr, dist, rare)
  local count, total, before = {}, 0, {}
  for ci, cel in ipairs(spr.cels) do
    before[ci] = cel.image:clone()
    for it in cel.image:pixels() do
      local v = it()
      if pc.rgbaA(v) < 128 then it(0) else v = opaque(v); it(v); count[v] = (count[v] or 0) + 1; total = total + 1 end
    end
  end
  local cols = {}
  for c in pairs(count) do cols[#cols + 1] = c end
  table.sort(cols, function(a, b) return count[a] > count[b] or (count[a] == count[b] and a < b) end)
  -- common colours are all kept first, so a rare colour can never block a later merge (keeps a second run a no-op)
  local limit, keep, map, maxd = math.max(3, rare * total), {}, {}, 0
  for _, c in ipairs(cols) do if count[c] >= limit then keep[#keep + 1] = c end end
  for _, c in ipairs(cols) do
    if count[c] < limit then
      local best, bd = nil, dist * dist
      for _, k in ipairs(keep) do local e = d2(c, k); if e <= bd then best, bd = k, e end end
      if best then map[c] = best; maxd = math.max(maxd, math.sqrt(bd)) else keep[#keep + 1] = c end
    end
  end
  local changed, isolated, after = 0, 0, {}
  for ci, cel in ipairs(spr.cels) do
    local img = cel.image
    for it in img:pixels() do
      local v = it(); if map[v] then it(map[v]) end
      local o = before[ci]:getPixel(it.x, it.y) -- invisible junk RGB under alpha 0 is cleared but not counted
      if o ~= it() and (pc.rgbaA(o) > 0 or it() ~= 0) then changed = changed + 1 end
    end
    for it in img:pixels() do
      local v = it()
      if pc.rgbaA(v) > 0 then
        after[v] = (after[v] or 0) + 1
        local alone = true
        for dy = -1, 1 do for dx = -1, 1 do
          local x, y = it.x + dx, it.y + dy
          if (dx ~= 0 or dy ~= 0) and x >= 0 and y >= 0 and x < img.width and y < img.height and pc.rgbaA(img:getPixel(x, y)) > 0 then alone = false end
        end end
        if alone then isolated = isolated + 1 end
      end
    end
  end
  local ncol, few = 0, 0
  for _, n in pairs(after) do ncol = ncol + 1; if n <= 3 then few = few + 1 end end
  local img, doubled = spr.cels[1].image, spr.width % 2 == 0 and spr.height % 2 == 0
  for y = 0, img.height - 2, 2 do for x = 0, img.width - 2, 2 do
    local v = img:getPixel(x, y)
    if doubled and (img:getPixel(x + 1, y) ~= v or img:getPixel(x, y + 1) ~= v or img:getPixel(x + 1, y + 1) ~= v) then doubled = false end
  end end
  return { before = #cols, after = ncol, changed = changed, maxd = maxd, isolated = isolated, few = few, doubled = doubled, orig = before[1] }
end

local function selftest()
  local s = Sprite(12, 12)
  local img = s.cels[1].image
  local A, B, C = pc.rgba(200, 40, 40, 255), pc.rgba(205, 44, 40, 255), pc.rgba(201, 41, 41, 255)
  for y = 0, 3 do for x = 0, 9 do img:drawPixel(x, y, A) end end   -- 40 px of A
  for y = 4, 7 do for x = 0, 9 do img:drawPixel(x, y, B) end end   -- 40 px of B, close to A: both common, both kept
  img:drawPixel(10, 0, C)                                          -- 1 px close to A: merged
  for i = 0, 3 do img:drawPixel(8 + i, 8 + i, pc.rgba(0, 0, 255, 255)) end -- diagonal line, 4 px
  img:drawPixel(0, 11, pc.rgba(9, 9, 9, 100))                       -- semi alpha: dropped
  local r = clean(s, 8, 0.015)
  assert(r.before == 4 and r.after == 3, "colours " .. r.before .. "->" .. r.after)
  assert(img:getPixel(10, 0) == A and img:getPixel(0, 5) == B, "merge kept ramp / took rare")
  assert(img:getPixel(0, 11) == 0, "semi alpha not cleared")
  assert(img:getPixel(9, 9) == pc.rgba(0, 0, 255, 255) and r.isolated == 0, "diagonal line touched or counted isolated")
  assert(clean(s, 8, 0.015).changed == 0, "second run not a no-op")
  print("selftest ok")
end

local p = app.params
if p.selftest then selftest() return end
-- oneFrame: app.open would load b1.png..b4.png as one 4-frame sequence and save over all of them
local spr = Sprite{ fromFile = p.src, oneFrame = true }
assert(spr and spr.colorMode == ColorMode.RGB and #spr.frames == 1, "need a single-frame RGB image: " .. tostring(p.src))
local r = clean(spr, tonumber(p.dist or 8), tonumber(p.rare or 0.015))
print(string.format("%s cols %d->%d changed=%d maxd=%.1f isolated=%d few=%d%s", p.src, r.before, r.after, r.changed,
  r.maxd, r.isolated, r.few, r.doubled and " DOUBLED?" or ""))
if p.preview then
  local w, h = spr.width, spr.height
  local v = Sprite(w * 2 + 2, h)
  v.cels[1].image:drawImage(r.orig, Point(0, 0))
  v.cels[1].image:drawImage(spr.cels[1].image, Point(w + 2, 0))
  v:resize(v.width * 4, v.height * 4)
  v:saveCopyAs(p.preview)
end
if p.apply == "1" and r.changed > 0 then spr:saveCopyAs(p.src) end
