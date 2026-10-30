"""Pure historical recovery-thunk fixture for native regression tests; no process access."""
import struct
def jump(address):return bytes.fromhex('ff2500000000')+struct.pack('<Q',address)

RVA = 0x35ff940
ORIGINAL = bytes.fromhex('c5fa114c2410554157488d6c24b1')

def build(data, base, state_object):
    body = bytearray()
    labels = {}
    fixups = []
    def emit(text): body.extend(bytes.fromhex(text))
    def address(text, value): emit(text); body.extend(struct.pack('<Q', value))
    def branch(text, label):
        emit(text); fixups.append((len(body), label)); body.extend(bytes(4))
    emit('9c5051524150415141524153')
    address('49ba', data)
    emit('41833a01'); branch('0f85', 'native')
    address('48b8', state_object)
    emit('483bc8'); branch('0f85', 'native')
    address('48b8', base + 0xf85f5f0)
    emit('488b0083b8a000000005'); branch('0f85', 'native')
    emit('41c702000000004881ec88000000')
    for x in range(6):
        emit('f30f7f'); body.append(0x84+x*8); emit('24'); body.extend(struct.pack('<I',32+x*16))
    address('48b9',base+0xdae5480)
    address('48b8',base+0x7baf970)
    emit('ffd0')
    address('49ba',data); emit('41c7420401000000')
    for x in range(6):
        emit('f30f6f'); body.append(0x84+x*8); emit('24'); body.extend(struct.pack('<I',32+x*16))
    emit('4881c488000000')
    labels['native']=len(body)
    emit('415b415a415941585a59589d')
    body.extend(ORIGINAL);body.extend(jump(base+RVA+len(ORIGINAL)))
    for offset,label in fixups:struct.pack_into('<i',body,offset,labels[label]-offset-4)
    return bytes(body)
