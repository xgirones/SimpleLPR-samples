import sys, os, argparse

# Import the SimpleLPR extension.
import simplelpr

# Lists all available countries.

def list_countries(eng):
    print('List of available countries:')

    for i in range(0, eng.numSupportedCountries):
        print(eng.get_countryCode(i))


def analyze_file(eng, country_id, img_path, key_path):
    # Enables syntax verification with the selected country.
    eng.set_countryWeight(country_id, 1)
    eng.realizeCountryWeights()

    # If provided, supplies the product key as a file.
    if key_path is not None:
        eng.set_productKey(key_path)

    # Alternatively, it could also be supplied from a buffer in memory:
    #
    # with open(key_path, mode='rb') as file:
    #     key_content = file.read()
    # eng.set_productKey( key_content )

    # Create a Processor object. Every working thread should use its own processor.
    proc = eng.createProcessor()

    # Enable the plate region detection and crop to plate region features.
    proc.plateRegionDetectionEnabled = True
    proc.cropToPlateRegionEnabled = True

    # Looks for license plate candidates in an image in the file system.
    cds = proc.analyze(img_path)

    # Alternatively, the input image can be supplied through an object supporting the buffer protocol:
    #
    # fh = open(img_path, 'rb')
    # try:
    #     ba = bytearray(fh.read())
    # finally:
    #     fh.close()
    # cds = proc.analyze(ba)
    #
    # or	
    #
    # import numpy as np
    # from PIL import Image
    #
    # im = Image.open(img_path)
    # npi = np.asarray(im)
    # cds = proc.analyze(npi)
    #
    # or
    #
    # import cv2
    #
    # im = cv2.imread(img_path)
    # cds = proc.analyze(im)

    # Show the detection results.
    print('Number of detected candidates:', len(cds))

    for cand in cds:
        print('-----------------------------')
        print('darkOnLight:', cand.darkOnLight, ', plateDetectionConfidence:', cand.plateDetectionConfidence)
        print('boundingBox:', cand.boundingBox)
        print('plateRegionVertices:', cand.plateRegionVertices)

        for cm in cand.matches:
            print('\tcountry:', "'{:}'".format(cm.country), ', countryISO:', "'{:}'".format(cm.countryISO),
                  ', text:', "'{:}'".format(cm.text), ', confidence:', '{:.3f}'.format(cm.confidence))

            for e in cm.elements:
                print('\t\tglyph:', "'{:}'".format(e.glyph), ', confidence:', '{:.3f}'.format(e.confidence),
                      ', boundingBox:', e.boundingBox)


def main():
    try:

        # The simplelpr extension requires 64-bit Python 3.8, 3.9, 3.10 or 3.11

        supported_python_versions = [(3, 8), (3, 9), (3, 10), (3, 11)]

        if sys.version_info[0:2] not in  supported_python_versions:
            raise RuntimeError('Apologies for the inconvenience, but to function properly this demo requires any of the following Python versions: ' + ' '.join([str(item[0]) + '.' + str(item[1]) + ' ' for item in  supported_python_versions]))

        if len(sys.argv) == 1:
            sys.argv.append('--help')


        # Create a SimpleLPR engine.

        setupP = simplelpr.EngineSetupParms()
        eng = simplelpr.SimpleLPR(setupP)

        print("SimpleLPR version:",
              "{:}.{:}.{:}.{:}".format(eng.versionNumber.A, eng.versionNumber.B, eng.versionNumber.C,
                                       eng.versionNumber.D))

        # Parse the command line arguments.

        parser = argparse.ArgumentParser(description='SimpleLPR on Python demo application')
        subparsers = parser.add_subparsers(dest='command', help='Sub-command help')
        subparsers.add_parser('list', help='List all available countries')
        parser_analyze = subparsers.add_parser('analyze', help='Looks for license plate candidates in an image')
        parser_analyze.add_argument('country_id', type=str, help='Country string identifier')
        parser_analyze.add_argument('img_path', type=str, help='Path to the image file')
        parser_analyze.add_argument('key_path',
                                    type=str,
                                    nargs='?',
                                    help="Path to the registration key file. In case you need to extend the 60-day "
                                         "evaluation period you can send an e-mail to 'support@warelogic.com' to "
                                         "request a trial key")

        args = parser.parse_args()

        if args.command == 'list':
            # List countries.
            list_countries(eng)
        elif args.command == 'analyze':
            # Analyze an image in the file system.
            analyze_file(eng, args.country_id, args.img_path, args.key_path)
        else:
            # Shouldn't occur.
            raise RuntimeError('Unknown command')

    except Exception as e:
        print('An exception occurred: {}'.format(e))


if __name__ == '__main__':
    main()