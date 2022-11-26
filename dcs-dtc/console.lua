function parse_indication(indicator_id)  -- Thanks to [FSF]Ian code
	local t = {}
	local li = list_indication(indicator_id)
	local m = li:gmatch("-----------------------------------------\n([^\n]+)\n([^\n]*)\n")
	while true do
    	local name, value = m()
    	if not name then break end
   			t[name]=value
	end
	return t
end
------------------------------------
function FgToString(o, iDepth, iDepthMax)
    iDepthMax = iDepthMax or 20
    iDepth = iDepth or 0

    if (iDepth > iDepthMax) then
        return ""
    end

    local sString = ""
    if (type(o) == "table") then
        sString = "\n"
        for key, value in pairs(o) do
            for i = 0, iDepth do
                sString = sString .. " "
            end
            sString = sString .. "." .. key .. "=" .. FgToString(value, iDepth + 1, iDepthMax) .. "\n"
        end
    elseif (type(o) == "function") then
        sString = "[function]"
    elseif (type(o) == "boolean") then
        if o == true then
            sString = "[true]"
        else
            sString = "[false]"
        end
    else
        if o == nil then
            sString = "[nil]"
        else
            sString = tostring(o)
        end
    end
    return sString
end

------------------------------------

local t = {}
for key, value in pairs(_G) do
	t[#t+1] = key
end
return t

------------------------------------
return list_indication(3)
return FgToString(getmetatable(GetDevice(35)))