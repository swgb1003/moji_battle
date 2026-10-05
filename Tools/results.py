import sys, xml.etree.ElementTree as ET
r=ET.parse(sys.argv[1]).getroot()
print(r.attrib.get('result'), 'passed',r.attrib.get('passed'),'failed',r.attrib.get('failed'), 'duration', r.attrib.get('duration'))
for tc in r.iter('test-case'):
    print(' ', tc.attrib['result'], tc.attrib['name'], tc.attrib.get('duration'))
    if tc.attrib['result']!='Passed':
        m=tc.find('.//message'); print('    ', (m.text if m is not None else '').strip()[:600])
