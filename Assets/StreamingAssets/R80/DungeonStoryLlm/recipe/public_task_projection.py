# Reachable R80 inference definitions. See recipe-provenance.json.
import copy

def project_contract(contract, header_lines):
    offers = copy.deepcopy(contract['offers'])
    for offer in offers:
        axes = dict((p.split('=', 1) for p in offer.get('mechanics', '').split('; ') if '=' in p))
        if axes.get('target') in ('Self', 'Ally', 'Enemy'):
            offer['explicitFutureRecipient'] = {'Self': '자신', 'Ally': '아군', 'Enemy': '적'}[axes['target']]
    return dict(originalPlainHeaders=list(header_lines), profile=contract['profile'], requestRef=contract['requestRef'], characterName=contract['characterName'], background=[copy.deepcopy(d['background']) for d in contract['publicDocuments'] if 'background' in d], offers=offers, modules=copy.deepcopy(contract['modules']), facts={k: {a: b for a, b in sorted(copy.deepcopy(v).items())} for k, v in sorted(contract['facts'].items())})
